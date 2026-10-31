using Microsoft.AspNetCore.Identity;

namespace POSWebApi.Models
{
    public class AppUser:IdentityUser<int>
    {
        //public Customer? Customer { get; set; }

        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
