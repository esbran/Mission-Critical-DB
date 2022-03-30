# CosmosDB

Optimizing CosmosDB for mission critical applications.

## Designing a database

When you deploy Cosmos DB it consists of one account. Each account can have multiple databases and every database can have one to many containers. CosmosDBs execution capacity unit us Request Units (RU's). RU usage can be dedicated to the Database Scope to be shared by the underlying Containers, or can be dedicated to a single Container scope. Independent from whether you consume RU's on Database wide or Container level, when assigning RU usage behavior there two options  you can either assign a fixed number of Request Units (RU's)  or you choose to let Microsoft manage the Autoscaling between customer decided minimum n-RU's and maximum n/10-RU's.
If you chose the latter the infrastructure supporting the maximum capacity is created upfront. If you chose the former the infrastructure gets provisioned by the manual scale operations that may be performed upon need. In cosmos DB the effect of scale up on underlying infrastructure is irreversible. Cosmos DB scales by adding new compute nodes to the existing cluster to support required data size or RU capacity. The given RU capacity is always equally shared by the existing compute nodes. For more information on scaling CosmosDB you can refer to the this article [Background on scaling RU's](https://docs.microsoft.com/azure/cosmos-db/scaling-provisioned-throughput-best-practices#background-on-scaling-rus) on Microsoft CosmosDB documentation.

When creating a new database and you don't know the load, it is recommended to monitor this closely and adjust If you have somewhat an idea you can use [CosmosDB Capacity Calculator](https://cosmos.azure.com/capacitycalculator/) to have a rough estimate as a better chosen starting point. Starting small and then scaling up upon need by monitoring the application can be is a good practice.

Cosmos DB exposes a set of API's. Each API has different functionality and SDK's.

* Core(SQL) API
This API stores data in document format. It offers the best end-to-end experience as we have full control over the interface, service, and the SDK client libraries. Any new feature that is rolled out to Azure Cosmos DB is first available on SQL API accounts.
* API for MongoDB
This API stores data in a document structure, via BSON format. It is compatible with MongoDB wire protocol(up to 4.0).
* Cassandra API
This API stores data in column-oriented schema. Apache Cassandra is a highly distributed, horizontally scaling approach to storing large volumes of data while offering a flexible approach to a column-oriented schema.
* Gremlin API
This API allows users to make graph queries and stores data as edges and vertices. Use this API for scenarios involving dynamic data, data with complex relations, data that is too complex to be modeled with relational databases, and if you want to use the existing Gremlin ecosystem and skills.
* Table API
This API stores data in key/value format.

The remainder of this article will focus on the Core SQL API.

## Distribution

Each database consists of n number of Container. Each container can have one or many logical partitions and physical partitions hosting the logical partitions . Physical partitions are the nodes Cosmos DB is built on and are automatically provisioned as your data size or throughput grows. Each physical partition can contain up tos 20 GB of data and has a maximum throughput capacity of 10k RU's. Each physical partition can have  n number of logical partitions, but a logical partition can not grow larger than a physical partition, hence 1 logical partition can be hosted only on 1 physical partition while 1 physical partition can host n logical partitions. When creating new containers if the given initial RU is more than around 8K the CosmosDB gets provisioned as more than 1 physical partition, where each partition can have an RU value between 4k & 6k RU's and this allow you to grow to 10k without having to reshuffle.  So if your CosmosDB has only 1 physical partition it can grow to 10K RUs without provisioning new nodes, but if you create your CosmosDB starting with 10K RU's you will end up with at least 2 physical partition each using 5K RU's and the CosmosDB  can scale up to 20K RU's without any new node provisioning.

### Selecting a partition key

When designing your Cosmos DB it is essential to chose the right partition key to be able to achieve performance and scalability. You have three options; Single value key, Synthetic key or Hierarchial keys(this feature is in preview). Cosmos DB uses the hash of the partition key value to determine which logical and physical partition the data lives on. Ideally you want to have an even distribution of logical partitions  by size and read requests as possible, with documents and RU usage spread evenly across partitions.
The most efficient way to retrieve a document is by using the documents unique ID and the partition key. This will result in a "point read". For read heavy workloads choose a key that is part of the filter. To add more cardinality to our key, we can use a synthetic key, combining multiple
attributes. which would require the queries to do the the same combination operation. The cons of building a synthetic key is that you can not benefit from it for individual pieces of the synthetic key.

```sql
SELECT * FROM n WHERE "Partition_Key" = 123 
```

Multi item transactions has to be performed on the same logical partition in other words, ACID transactions are only supported in a single container-logical partition scope. For more information on multi item transaction you can refer to [Multi Item Transactions on CosmosDB](https://docs.microsoft.com/azure/cosmos-db/sql/database-transactions-optimistic-concurrency#multi-item-transactions) article.

### Hierarchical partition key

With hierarchical partition keys, you can partition your container with up to three levels of partition keys.

```json
|- PartitionKey1   -> Tenant
    |- PartitionKey2 -> User
        |- PartitionKey3 -> OrderDate
```

for example

```json
|- Tenant
    |-  User
        |-  OrderDate
```

Using hierarchical partition keys, it is now possible to retrieve data that spans multiple physical partitions with using higher level keys in the hierarchy. 

At the same time, the queries will still be efficient. For example  all queries that target a single Tenant will be routed to the subset of physical partitions that the data is on, avoiding the full cross-partition fanout query that would be required when using a synthetic or regular partition key strategy.
Some designs may make useful putting different documents into one container. in such cases, both documents must have all the partition key columns in the same json level. Hierarchical partition keys would not function if the keys are in different jason levels in different documents like one document having the keys as nested jsons while the other as a plain json.

* Hierarchical partition keys are in private preview *

### Design Recommendations

* Select a partition key that will ensure your logical partition always stay smaller than the physical partition limits.
* Partition keys should be immutable and have a high cardinality.
* Use hierarchial distribution keys for large datasets to get an even distribution of documents and throughput across the logical and physical partitions of your container.

## Document structure

### No-SQL denormalization

In these two examples you’ll see denormalization of the data model. Option 1 has separated transactions and use a weak relational link between the entities using the unique Id. The second (denormalized) example doesn’t need a second lookup in the client container. You can just fetch the document and have the transactions embedded in the document.
With the denormalized model each container can be scaled independently.

#### Option 1 - two separate containers

Client document:

```json
{
    "clientId": "",
    "wallet": [
        {
            "walletId": "",
            "bookBalance": "",
            "reservedBalance": "",
            "subwallet": [
                {
                    "subwalletId": "",
                    "balance": ""
                }
            ]
        }
    ],
    "card": [
        {
            "cardNumber": ""
        }
    ]
}
```

Transaction document:

```json
{
    "transactionId":"",
    "transactionData":"",
    "cardNumber":"",
    "walletId":"",
    "subwalletId":""
}
```

#### Option 2 - Nested documents

The two documents in the previous example are merged into a nested document. This can be done to ensure they are both in the same logical partition. Be aware that this can create very large documents and reduce performance on CRUD operations. Every time you update anything og add a new nested sub-document like a transaction you have to fetch the entire document.

```json
{
    "clientId": "",
    "wallet": [
        {
            "walletId": "",
            "bookBalance": "",
            "reservedBalance": "",
            "subwallet": [
                {
                    "subwalletId": "",
                    "balance": "",
                    "transaction": [
                        {
                            "transactionId": "",
                            "transactionData": "",
                            "cardNumber": ""
                        }
                    ]
                }
            ]
        }
    ],
    "card": [
        {
            "cardNumber": ""
        }
    ]
}
```

### Design recommendations

* Consider the size of each document and the nesting level. Avoid large documents.
* Optimize document structure for query patterns.
* Denormalization is not always the best option.

## ACID transactions

ACID (Atomicity, Consistency, Isolation, Durability) is a set of properties of database transactions intended to guarantee data validity. Cosmos DB supports full ACID compliant transactions with snapshot isolation. All the database operations a logical partition are transactionally executed within the database engine that is hosted by the replica of the partition. These operations include all CRUD operations of one or multiple items. Stored procedures, triggers, UDFs, and merge procedures are ACID transactions with snapshot isolation across all items within the same logical partition. During the execution, if the program throws an exception, the entire transaction is aborted and rolled-back.

### Design recommendation

* ACID transactions are confined to a logical partition. All data managed by the transaction has to reside in the same logical partition.

### Design considerations

* An alternative solution if the application needs to update multiple containers can be to listen to Cosmos DB change feed and create an event trigger to update the different containers. This will involve multiple chained transactions.

## Indexing

All fields in a document are indexed automatically. Cosmos DB has 3 different types of indexes.

* Range index
  * Automatically added to all objects
* Spatial index
  * Used for Geospatial data
* Composite
  * Used for Complex filters

Cosmos DB is schema-less and index all your data regardless of the data model. This is the Range-index.

You can define custom indexing policies for your containers. This allows you to improve query performance and consistency. Index policies are defined as the example below with including and excluding paths. You can also specify document attributes, data types and ordering sequence.
When you change your indexing policy the new index build operation utilizes your unused RUs without effecting your existing workload.

```csharp
IndexingPolicy indexingPolicy = new IndexingPolicy
{
    IndexingMode = IndexingMode.Consistent,
    Automatic = true,
    IncludedPaths =
    {
        new IncludedPath
        {
            Path = "/*"
        }
    },
    ExcludedPaths =
    {
        new ExcludedPath
        {
            Path = "/ignore"
        }
    }
};
```

### Design recommendations

* Use the metrics analysis with information about storage and throughput to optimize your indexing policies.
* Index policies are applied to an entire container, but will be executed on each partition.

## Consistency and availability

| Region(s) | Mode | Consistency | RPO | RTO |
|:-----------|:--------|--------|--------------|--------|
| 1 | Any | Any | < 240 minutes | < 1 week |
| > 1 | Single Write | Session, Consistent Prefix, Eventual | < 15 minutes | < 15 minutes |
| > 1 | Single Write | Bounded Staleness | K & T* | < 15 minutes |
| > 1 | Single Write | Strong | 0 | < 15 minutes |
| > 1 | Multi Write | Session, Consistent Prefix, Eventual | < 15 minutes | 0 |
| > 1 | Multi Write | Bounded Staleness | K & T* | 0 |
| > 1 | Single Write | Strong | N/A | N/A |

*Number of K updates of an item or T time. In >1 regions, K=100,000 updates or T=5 minutes.

## Synapse Link

Analytical workloads are very different from transactional workloads and require different tools to store and query the data.

```sql
SELECT * FROM A UNION SELECT * FROM B
vs
SELECT A FROM N WHERE id = 123
```

When setting up Synapse Link for Cosmos DB it has an independent TTL - Time to live, Analytical data can live forever and the more expensive transactional data can have a shorter retention.

* The analytical TTL is set at container level, each container can have different TTL's.

* The data exported to the data lake using a highly compressed Parquet format - up to 90% reduction in size.
* No cost of replicating the data and no impact on the transactional store, no RU's used
* Cheaper to run queries in Synapse than in CosmosDB
* Spark connector to write data back to CosmosDB
* Data is synced within 2 min, all CRUD operations are performed on the parquet files
* Automatic Schema inference
  * Schema inference type can only be set at creation time and not changed
  * Well-Defined schema - Data type pr column. One column for each entity. "wrong" type represented as NULL ( you loose data of wrong type.
  * Full-Fidelity - Multiple columns (pr data type) for each entity. Missing data represented as NULL

Analytical store will be available in all locations Cosmos DB is provisioned.
Synapse will default to the primary region, but you can change to any region available

### Custom partitioning

Custom partitioning on Synapse Link created using Synapse Spark - Partitioned Store

* Only on SQL atm (MongoDB soon)
* Jobs are schedule
* Improved query performance.
* Can create multiple partition stores optimized for each query plan
* Partition pruning - Now you scan less data and can save cost pr query
* Default Synapse Link with slow data ingestion gets fragmented, partitioning defragments it.
* Reads and updates the Delta only
* Customer define frequency, path  in ADLS, partition key
* Define number of records pr file, recommend creating bigger files ~2 mill
* Any property in a nested JSON can be used as a partition key
* The first document defines the schema.
  * If your first doc is wrong or bugged, it has to be removed for a new schema to be created.
  