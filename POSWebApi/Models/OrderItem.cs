using Microsoft.EntityFrameworkCore;

namespace POSWebApi.Models
{
    public class OrderItem
    {
        public int OrderItemId { get; set; }

        public int OrderId { get; set; }

        public int ProductId { get; set; }

        public int Quantity { get; set; }

        [Precision(18, 2)]
        public decimal UnitPrice { get; set; }

        [Precision(18, 2)]
        public decimal Discount { get; set; }

        [Precision(18, 2)]
        public decimal Subtotal { get; set; }

        // Navigation Properties

        public Order Order { get; set; } = null!;

        public Product Product { get; set; } = null!;

        public ICollection<ReturnItem> ReturnItems { get; set; }
            = new List<ReturnItem>();
    }
}
