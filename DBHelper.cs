using System;
using System.Threading.Tasks;
using System.Configuration;
using System.Collections.Generic;
using System.Net;
using Microsoft.Azure.Cosmos;

namespace Viva
{
    public class DBHelper
    {
        private CosmosClient cosmosClient;
        private Database database;
        private Container container;
        private bool preview = false; 
        private string databaseId;
        private static readonly string EndpointUri = ConfigurationManager.AppSettings.Get("EndpointUri");
        private static readonly string PrimaryKey = ConfigurationManager.AppSettings.Get("PrimaryKey");
        public DBHelper(string Name)
        {
            this.cosmosClient = new CosmosClient(EndpointUri, PrimaryKey, new CosmosClientOptions() { ApplicationName = Name });
        }
        public DBHelper(string Name, bool preview)
        {
            this.cosmosClient = new CosmosClient(EndpointUri, PrimaryKey, new CosmosClientOptions() { ApplicationName = Name });
            this.preview = preview;
        }
        public async Task CreateDatabaseAsync(string DatabaseName)
        {
            databaseId = DatabaseName;

            this.database = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            Console.WriteLine("Created Database: {0}\n", this.database.Id);
        }
        public async Task CreateCosmosContainers(string level1, string level2, string level3, string containerName)
        {
            this.database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            ContainerProperties containerProperties;
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
                }
            };

            if(preview)
            {
                List<string> subpartitionKeyPaths = new List<string> { level1, level2, level3 };
                containerProperties = new ContainerProperties(id: containerName, partitionKeyPaths: subpartitionKeyPaths)
                {
                    IndexingPolicy = indexingPolicy
                };

            } 
            else 
            {
                string synethicKey = string.Concat(level1, level2, level3);
                containerProperties = new ContainerProperties(id: containerName, partitionKeyPath: synethicKey )
                {
                    IndexingPolicy = indexingPolicy
                };
            }
            container = await database.CreateContainerIfNotExistsAsync(containerProperties, throughput: 400);
            Console.WriteLine("Container created: {0}\n", this.container.Id);
        }

        public async Task CreateTransactionRecord(string db, string cont, PaymentTransactions transRecord)
        {
            Container transactioncontainer = cosmosClient.GetDatabase(db).GetContainer(cont);
            
            if(preview)
            {
                PartitionKey partitionKey = new PartitionKeyBuilder()
                .Add(transRecord.transactionId.ToString())
                .Add(transRecord.walletId)
                .Add(transRecord.subwalletId)
                .Build();
                ItemResponse<PaymentTransactions> itemResponse1 = await transactioncontainer.CreateItemAsync(transRecord, partitionKey);
            }
            else
            {
                string synethicKey = string.Concat(transRecord.transactionId.ToString(), transRecord.walletId, transRecord.subwalletId);
                transRecord.id = synethicKey;
                ItemResponse<PaymentTransactions> itemResponse2 = await transactioncontainer.CreateItemAsync(transRecord);
            }
        }
        public async Task CreateWallets(string db, string cont, Client clientRecord)
        {
            Container walletcontainer = cosmosClient.GetDatabase(db).GetContainer(cont);

            if(preview)
            {
                PartitionKey partitionKey = new PartitionKeyBuilder()
                .Add(clientRecord.clientId)
                .Add(clientRecord.wallets[0].walletId)
                .Add(clientRecord.wallets[0].subwallets[0].subWalletId)
                .Build();
                ItemResponse<Client> itemResponse1 = await walletcontainer.CreateItemAsync(clientRecord, partitionKey);
            }
            else
            {
                string synethicKey = string.Concat(clientRecord.clientId.ToString(), clientRecord.wallets[0].walletId, clientRecord.wallets[0].subwallets[0].subWalletId);
                clientRecord.id = Guid.NewGuid().ToString();
                ItemResponse<Client> itemResponse2 = await walletcontainer.CreateItemAsync(clientRecord);
            }
        }
    }
}