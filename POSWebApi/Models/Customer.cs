// using System.ComponentModel.DataAnnotations;

// namespace POSWebApi.Models
// {
//     public class Customer
//     {
//         public int CustomerId { get; set; }

//         [Required]
//         [MaxLength(150)]
//         public string CustomerName { get; set; } = string.Empty;

//         [MaxLength(20)]
//         public string? Phone { get; set; }

//         public DateTime CreatedAt { get; set; } = DateTime.Now;       
//         public int? UserId { get; set; }   
//         public AppUser? User { get; set; }

//         public ICollection<Order> Orders { get; set; }
//             = new List<Order>();
//     }
// }
