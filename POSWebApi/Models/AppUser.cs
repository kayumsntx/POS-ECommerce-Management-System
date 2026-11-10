using Microsoft.AspNetCore.Identity;

namespace POSWebApi.Models
{
    public class AppUser : IdentityUser<int>
    {
        public string? FullName { get; set; }

        public string? DefaultAddress { get; set; }

        public string? AvatarUrl { get; set; }

        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
