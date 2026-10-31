using Microsoft.EntityFrameworkCore;

namespace POSWebApi.Models
{
    public class Product
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string Barcode { get; set; } = string.Empty;

        public int CategoryId { get; set; }
        [Precision(18,2)]
        public decimal Cost { get; set; }
        [Precision(18, 2)]
        public decimal Price { get; set; }


        public string? ImageUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public Category Category { get; set; } = null!;

        //public Inventory? Inventory { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; }
            = new List<OrderItem>();
    }
}
