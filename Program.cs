using System;
using System.Threading.Tasks;
using System.Configuration;
using System.Collections.Generic;
using System.Net;
using Microsoft.Azure.Cosmos;

namespace Viva
{
    public class Program
    {
        public static bool preview = false;
        private static string DatabaseName = "VivaDB";
        public static async Task Main(string[] args)
        {
            try
            {
                Program p = new Program();
                await p.CreateDatabase();

            }
            catch (CosmosException cosmosException)
            {
                Console.WriteLine("Cosmos Exception with Status {0} : {1}\n", cosmosException.StatusCode, cosmosException);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: {0}", e);
            }

        }
        public async Task CreateDatabase()
        {
            DBHelper dbh;
            if(preview)
            {
                dbh = new DBHelper(DatabaseName, true);
            }
            else
            {
                dbh = new DBHelper(DatabaseName);
            }
            await dbh.CreateDatabaseAsync(DatabaseName);            
            await dbh.CreateCosmosContainers("/walletId", "/subwalletId", "/transactionDate", "Transactions");        
            await CreateTransactions(dbh);
            await dbh.CreateCosmosContainers("/clientId", "/walletId", "/subwalletId", "Wallets");  
            await CreateClients(dbh);
        }

        private async Task CreateTransactions(DBHelper dbh)
        {

            for(int i=0; i<1000; i++)
            {
                Random gen = new Random();
                PaymentTransactions transactions = new PaymentTransactions()
                {
                    transactionDate = RandomDay().ToString(),
                    transactionId = Guid.NewGuid(),
                    cardNumber = string.Format("{0}",i*10000*3.14),
                    walletId = gen.Next(1,100).ToString(),
                    subwalletId = "1"
                };
               await dbh.CreateTransactionRecord(DatabaseName, "Transactions", transactions);
            }
        }

        private async Task CreateClients(DBHelper dbh)
        {
            for(int i=0; i<100; i++)
            {
                Random gen = new Random();
                Card card = new Card()
                {
                    cardnumber = string.Format("{0}",i*10000*3.14)
                };
                var cardlist = new List<Card>(1);
                cardlist.Add(card);
                Subwallet subWallet = new Subwallet()
                {
                    subWalletId = "1",
                    balance = gen.Next(10000, 999999)
                };
                var subwallist = new List<Subwallet>(1);
                subwallist.Add(subWallet);
                Wallet walletRecord = new Wallet()
                {
                    walletId = string.Format("{0}",i),
                    subwallets = subwallist,
                    cards = cardlist,
                    reservedBalance = subWallet.balance/10,
                    bookBalance = subWallet.balance
                };
                var wallist = new List<Wallet>(1);
                wallist.Add(walletRecord);
                
                Client client = new Client()
                {
                    clientId = string.Format("{0}",i*3.14),
                    wallets = wallist
                };
            
                await dbh.CreateWallets(DatabaseName, "Wallets", client);
            }
        }
        private static DateTime RandomDay()
        {
            Random gen = new Random();

            DateTime start = new DateTime(2022, 1, 1);
            int range = (DateTime.Today - start).Days;           
            return start.AddDays(gen.Next(range));
            

        }

    }
}