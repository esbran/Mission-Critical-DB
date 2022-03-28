
namespace Viva
{
    public class PaymentTransactions
    {
        public string id { get; set; }
        public Guid transactionId { get; set; }
        public string transactionDate { get; set; }
        public string cardNumber { get; set; }
        public string walletId { get; set; }
        public string subwalletId { get; set; }
    }

    public class Wallet
    {
        public string walletId { get; set; }
        public double bookBalance { get; set; }
        public double reservedBalance { get; set; }
        public List<Subwallet> subwallets { get; set; }
        public List<Card> cards { get; set; }
    }
    public class Client
    {
        public string id { get; set; }
        public string clientId { get; set; }
        public List<Wallet> wallets { get; set; }
    }

    public class Subwallet
    {
        public string subWalletId { get; set; }
        public double balance { get; set; }
    }

    public class Card
    {
        public string cardnumber { get; set; }
    }
}