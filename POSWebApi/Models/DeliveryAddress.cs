using System.ComponentModel.DataAnnotations;

namespace POSWebApi.Models
{
    public class DeliveryAddress
    {
        public int DeliveryAddressId { get; set; }

        public int OrderId { get; set; }

        [Required]
        [MaxLength(150)]
        public string RecipientName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Address { get; set; } = string.Empty;       
        public Order Order { get; set; } = null!;
    }
}
