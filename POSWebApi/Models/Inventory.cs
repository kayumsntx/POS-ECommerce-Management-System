namespace POSWebApi.Models
{
    public class Inventory
    {
        public int InventoryId { get; set; }

        public int ProductId { get; set; }

        public int Quantity { get; set; }

        //public int LocationId { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        //public Location Location { get; set; } = null!;
        public Product Product { get; set; } = null!;
    }
}
