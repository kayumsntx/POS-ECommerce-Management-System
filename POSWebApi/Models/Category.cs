using System.ComponentModel.DataAnnotations;

namespace POSWebApi.Models
{
    public class Category
    {
        public int CategoryId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }
        public ICollection<Product>? Products { get; set; } = new List<Product>();
    }
}
