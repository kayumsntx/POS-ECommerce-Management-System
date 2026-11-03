using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace POSWebApi.Models
{
    public class Order
    {
        public int OrderId { get; set; }        
        public string? Phone { get; set; }      
        public int? UserId { get; set; }        
        public bool IsOnlineOrder { get; set; }       
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        [Precision(18, 2)]
        public decimal TotalAmount { get; set; }
        [Precision(5, 2)]
        public decimal DiscountPercent { get; set; }

        [Precision(18, 2)]
        public decimal DiscountAmount { get; set; }

        [Precision(18, 2)]
        public decimal ShippingAmount { get; set; }

        [Precision(18, 2)]
        public decimal PayableAmount { get; set; }

        public PaymentMethodType PaymentMethod { get; set; } = PaymentMethodType.Cash;

        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

      
        [MaxLength(100)]
        public string? PaymentTransactionId { get; set; }

       
        public AppUser? User { get; set; }

       
        public DeliveryAddress? DeliveryAddress { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public ICollection<Payment>   Payments   { get; set; } = new List<Payment>();
        public ICollection<Return>    Returns    { get; set; } = new List<Return>();

        public string? CourierTrackingId { get; set; }
    }
}
