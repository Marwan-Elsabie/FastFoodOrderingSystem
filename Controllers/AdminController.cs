using FastFoodOrderingSystem.Data;
using FastFoodOrderingSystem.Models;
using FastFoodOrderingSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FastFoodOrderingSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;

        public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
        }

        public async Task<IActionResult> Dashboard()
        {
            var dashboardData = new AdminDashboardViewModel
            {
                TotalOrders = await _context.Orders.CountAsync(),
                TotalProducts = await _context.Products.CountAsync(),
                TotalRevenue = await _context.Orders.SumAsync(o => o.TotalAmount),
                PendingOrders = await _context.Orders.CountAsync(o => o.Status == "Pending"),
                RecentOrders = await _context.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .OrderByDescending(o => o.OrderDate)
                    .Take(10)
                    .ToListAsync()
            };

            return View(dashboardData);
        }

        public async Task<IActionResult> Orders()
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, string status, string returnUrl = null)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                return NotFound();
            }

            var previousStatus = order.Status;
            order.Status = status;
            await _context.SaveChangesAsync();

            // create audit log
            try
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = User.Identity?.Name ?? "Admin",
                    Action = "UpdateOrderStatus",
                    Entity = "Order",
                    EntityId = order.Id,
                    Details = $"Status changed from {previousStatus} to {status}"
                });
                await _context.SaveChangesAsync();
            }
            catch
            {
                // ignore audit errors
            }

            // notify user via email if email exists
            try
            {
                if (!string.IsNullOrEmpty(order.UserId))
                {
                    var user = await _userManager.FindByIdAsync(order.UserId);
                    var email = user?.Email;
                    var customerName = order.CustomerName ?? user?.UserName ?? "Customer";
                    if (!string.IsNullOrEmpty(email))
                    {
                        await _emailService.SendOrderStatusUpdateAsync(email, customerName, order.Id, status);
                    }
                }
            }
            catch
            {
                // don't block admin action for email failures
            }

            if (!string.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("OrderDetails", new { id = orderId });
        }

        public async Task<IActionResult> OrderDetails(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }
        public async Task<IActionResult> Analytics()
        {
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);

            var analytics = new AnalyticsViewModel
            {
                TotalCustomers = await _userManager.Users.CountAsync(),
                RevenueToday = await _context.Orders
                    .Where(o => o.OrderDate.Date == today)
                    .SumAsync(o => o.TotalAmount),
                RevenueThisMonth = await _context.Orders
                    .Where(o => o.OrderDate >= startOfMonth)
                    .SumAsync(o => o.TotalAmount),
                OrdersToday = await _context.Orders
                    .CountAsync(o => o.OrderDate.Date == today),

                DailyRevenues = await _context.Orders
                    .Where(o => o.OrderDate >= today.AddDays(-30))
                    .GroupBy(o => o.OrderDate.Date)
                    .Select(g => new DailyRevenue
                    {
                        Date = g.Key,
                        Revenue = g.Sum(o => o.TotalAmount)
                    })
                    .OrderBy(d => d.Date)
                    .ToListAsync(),

                TopProducts = await _context.OrderItems
                    .Include(oi => oi.Product)
                    .GroupBy(oi => oi.ProductId)
                    .Select(g => new TopProduct
                    {
                        ProductName = g.First().Product.Name,
                        QuantitySold = g.Sum(oi => oi.Quantity),
                        Revenue = g.Sum(oi => oi.Quantity * oi.UnitPrice)
                    })
                    .OrderByDescending(p => p.QuantitySold)
                    .Take(10)
                    .ToListAsync(),

                OrderStatusCounts = await _context.Orders
                    .GroupBy(o => o.Status)
                    .Select(g => new OrderStatusCount
                    {
                        Status = g.Key,
                        Count = g.Count()
                    })
                    .ToListAsync()
            };

            return View(analytics);
        }
    }
}