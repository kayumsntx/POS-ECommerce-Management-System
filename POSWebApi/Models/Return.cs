namespace POSWebApi.Models
{
    public class Return
    {
        public int ReturnId { get; set; }

        public int OrderId { get; set; }

        public DateTime ReturnDate { get; set; } = DateTime.UtcNow;

        public Order Order { get; set; } = null!;

        public ICollection<ReturnItem> ReturnItems { get; set; }
            = new List<ReturnItem>();
    }
}
