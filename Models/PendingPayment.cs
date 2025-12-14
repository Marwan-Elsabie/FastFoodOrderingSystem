using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // When the pending payment was processed (set by webhook). Nullable for idempotency checks.
        public DateTime? ProcessedAt { get; set; }
    }
}