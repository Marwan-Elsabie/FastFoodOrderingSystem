using System.ComponentModel.DataAnnotations;

namespace FastFoodOrderingSystem.Models
{
    public class ProfileViewModel
    {
        [EmailAddress]
        public string Email { get; set; }

        [Phone]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Delivery Address")]
        public string DeliveryAddress { get; set; }
    }
}