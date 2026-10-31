using System.ComponentModel.DataAnnotations;

namespace POSWebApi.Models
{
    public class ReturnItem
    {
        public int ReturnItemId { get; set; }

        public int ReturnId { get; set; }

        public int OrderItemId { get; set; }

        public int Quantity { get; set; }

        [MaxLength(500)]
        public string? ReturnReason { get; set; }

        // Navigation Properties

        public Return Return { get; set; } = null!;

        public OrderItem OrderItem { get; set; } = null!;
    }
}
