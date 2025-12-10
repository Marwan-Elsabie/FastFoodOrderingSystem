using System.ComponentModel.DataAnnotations;

namespace FastFoodOrderingSystem.Models
{
    public class PlaceOrderViewModel
    {
        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        [Display(Name = "Full name")]
        public string CustomerName { get; set; }

        [Required(ErrorMessage = "Delivery address is required")]
        [StringLength(500, ErrorMessage = "Address cannot exceed 500 characters")]
        [Display(Name = "Delivery address")]
        public string DeliveryAddress { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [StringLength(20, ErrorMessage = "Phone number is too long")]
        [Display(Name = "Phone number")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Payment method")]
        public string PaymentMethod { get; set; } = "CashOnDelivery";
    }
}