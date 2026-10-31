using System.ComponentModel.DataAnnotations;

namespace POSWebApi.Models
{
    public class Payment
    {
        public int PaymentId { get; set; }

        public int OrderId { get; set; }

        public int PaymentMethodId { get; set; }

        public decimal Amount { get; set; }

        [MaxLength(100)]
        public string? TransactionId { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.Now;

        // Navigation Properties
        public PaymentMethod PaymentMethod { get; set; } = null!;
        public Order Order { get; set; } = null!;

        //public ICollection<Refund>? Refunds { get; set; }
        //    = new List<Refund>();
    }
}
