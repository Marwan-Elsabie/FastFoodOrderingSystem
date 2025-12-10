using System;
using System.Collections.Generic;

namespace FastFoodOrderingSystem.Models
{
    public class AnalyticsViewModel
    {
        public int TotalCustomers { get; set; }
        public decimal RevenueToday { get; set; }
        public decimal RevenueThisMonth { get; set; }
        public int OrdersToday { get; set; }
        public List<DailyRevenue> DailyRevenues { get; set; }
        public List<TopProduct> TopProducts { get; set; }
        public List<OrderStatusCount> OrderStatusCounts { get; set; }
    }

    public class DailyRevenue
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
    }

    public class TopProduct
    {
        public string ProductName { get; set; }
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class OrderStatusCount
    {
        public string Status { get; set; }
        public int Count { get; set; }
    }
}