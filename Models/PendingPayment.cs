using System;
using System.ComponentModel.DataAnnotations;

namespace FastFoodOrderingSystem.Models
{
    public class PendingPayment
    {
        [Key]
        public int Id { get; set; }

        // Serialized cart JSON
        public string CartJson { get; set; }

        public string? UserId { get; set; }

        public string CustomerName { get; set; }

        public string DeliveryAddress { get; set; }

        public string PhoneNumber { get; set; }

        public decimal Amount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}