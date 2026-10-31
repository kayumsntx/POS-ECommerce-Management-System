//using System.ComponentModel.DataAnnotations;

//namespace POSWebApi.Models
//{
//    public class Refund
//    {
//        public int RefundId { get; set; }

//        public int ReturnId { get; set; }

//        public int PaymentId { get; set; }

//        public decimal Amount { get; set; }

//        public DateTime RefundDate { get; set; } = DateTime.Now;    

//        [MaxLength(100)]
//        public string? TransactionId { get; set; }

//        // Navigation Properties

//        public Return Return { get; set; } = null!;

//        public Payment Payment { get; set; } = null!;
//    }
//}
