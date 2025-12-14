using Microsoft.AspNetCore.Identity;

namespace FastFoodOrderingSystem.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
        public string? DeliveryAddress { get; set; }
    }
}