using System.Collections.Generic;

namespace FastFoodOrderingSystem.Models
{
    public class AdminDashboardViewModel
    {
        public int TotalOrders { get; set; }
        public int TotalProducts { get; set; }
        public decimal TotalRevenue { get; set; }
        public int PendingOrders { get; set; }

        // Recent orders shown on dashboard (includes navigation properties from Order)
        public List<Order> RecentOrders { get; set; } = new List<Order>();
    }
}