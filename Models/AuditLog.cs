using System;

namespace FastFoodOrderingSystem.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string Action { get; set; }
        public string Entity { get; set; }
        public int EntityId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Details { get; set; }
    }
}