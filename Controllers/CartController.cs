using FastFoodOrderingSystem.Data;
using FastFoodOrderingSystem.Helpers;
using FastFoodOrderingSystem.Models;
using FastFoodOrderingSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace FastFoodOrderingSystem.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IEmailService _emailService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<CartController> _logger;

        public CartController(
    ApplicationDbContext context,
    IHttpContextAccessor httpContextAccessor,
    UserManager<ApplicationUser> userManager,
    IEmailService emailService,
    ILogger<CartController> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _userManager = userManager;
            _emailService = emailService;
            _logger = logger;
        }

        private List<ShoppingCartItem> GetCartItems()
        {
            var cart = HttpContext.Session.GetObject<List<ShoppingCartItem>>("Cart") ?? new List<ShoppingCartItem>();
            return cart;
        }

        private void SaveCartItems(List<ShoppingCartItem> cart)
        {
            HttpContext.Session.SetObject("Cart", cart);
        }

        public IActionResult Index()
        {
            var cart = GetCartItems();
            return View(cart);
        }

        [HttpPost]
        public IActionResult AddToCart(int productId, int quantity = 1)
        {
            var product = _context.Products.Find(productId);
            if (product == null)
            {
                return NotFound();
            }

            var cart = GetCartItems();
            var existingItem = cart.FirstOrDefault(item => item.ProductId == productId);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                cart.Add(new ShoppingCartItem
                {
                    ProductId = productId,
                    Quantity = quantity,
                    Product = product
                });
            }

            SaveCartItems(cart);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult CartBadgePartialReload()
        {
            return PartialView("~/Views/Shared/_CartBadge.cshtml");
        }

        [HttpPost]
        public IActionResult UpdateCart(int productId, int quantity)
        {

            var cart = GetCartItems();
            var item = cart.FirstOrDefault(i => i.ProductId == productId);
            if (item == null)
            {
                return Json(new { success = false, message = "Item not found" });
            }

            if (quantity <= 0)
            {
                cart.Remove(item);
                SaveCartItems(cart);
                var cartTotalAfterRemoval = Math.Round(cart.Sum(i => i.Product.Price * i.Quantity), 2);
                return Json(new { success = true, itemTotal = 0m, cartTotal = cartTotalAfterRemoval, message = "Item removed" });
            }
            else
            {
                item.Quantity = quantity;
                SaveCartItems(cart);
                var itemTotal = Math.Round(item.Product.Price * item.Quantity, 2);
                var cartTotal = Math.Round(cart.Sum(i => i.Product.Price * i.Quantity), 2);
                return Json(new { success = true, itemTotal = itemTotal, cartTotal = cartTotal, message = "Cart updated" });
            }
        }

        [HttpPost]
        public IActionResult RemoveFromCart(int productId)
        {
            var cart = GetCartItems();
            var item = cart.FirstOrDefault(i => i.ProductId == productId);

            if (item != null)
            {
                cart.Remove(item);
                SaveCartItems(cart);
            }

            var cartTotal = Math.Round(cart.Sum(i => i.Product.Price * i.Quantity), 2);
            return Json(new { success = true, cartTotal = cartTotal, message = "Item removed" });
        }

        [HttpPost]
        public IActionResult ClearCart()
        {
            HttpContext.Session.Remove("Cart");
            return RedirectToAction("Index");
        }

        [Authorize]
        public async Task<IActionResult> Checkout()
        {
            var cart = GetCartItems();
            if (!cart.Any())
            {
                return RedirectToAction("Index");
            }

            ViewBag.Cart = cart;
            ViewBag.Total = cart.Sum(item => item.Product.Price * item.Quantity);

            // Prefill from profile where available
            var vm = new PlaceOrderViewModel
            {
                CustomerName = User.Identity?.Name ?? string.Empty,
                DeliveryAddress = string.Empty,
                PhoneNumber = string.Empty
            };

            if (User?.Identity?.IsAuthenticated ?? false)
            {
                try
                {
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (!string.IsNullOrEmpty(userId))
                    {
                        var user = await _userManager.FindByIdAsync(userId);
                        if (user != null)
                        {
                            // IdentityUser includes PhoneNumber by default
                            if (!string.IsNullOrEmpty(user.PhoneNumber))
                                vm.PhoneNumber = user.PhoneNumber;

                            // Prefer official user name when CustomerName is empty
                            if (string.IsNullOrEmpty(vm.CustomerName) && !string.IsNullOrEmpty(user.UserName))
                                vm.CustomerName = user.UserName;

                            // Try to read an address stored as a user claim (common patterns)
                            var claims = await _userManager.GetClaimsAsync(user);
                            var addressClaim = claims.FirstOrDefault(c =>
                                string.Equals(c.Type, "address", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(c.Type, "street_address", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(c.Type, "delivery_address", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(c.Type, "DeliveryAddress", StringComparison.OrdinalIgnoreCase));
                            if (addressClaim != null && !string.IsNullOrEmpty(addressClaim.Value))
                            {
                                vm.DeliveryAddress = addressClaim.Value;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to prefill checkout from user profile; continuing without prefill.");
                }
            }

            return View(vm);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(PlaceOrderViewModel model)
        {
            var cart = GetCartItems();
            if (!cart.Any())
            {
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Cart = cart;
                ViewBag.Total = cart.Sum(item => item.Product.Price * item.Quantity);
                return View("Checkout", model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = userId is not null ? await _userManager.FindByIdAsync(userId) : null;

            var products = await _context.Products
                .Where(p => cart.Select(c => c.ProductId).Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p);

            decimal total = 0m;
            foreach (var item in cart)
            {
                if (products.ContainsKey(item.ProductId))
                {
                    total += products[item.ProductId].Price * item.Quantity;
                }
            }

            if (model.PaymentMethod == "Card")
            {
                // Persist pending payment and redirect to Stripe Checkout
                var pending = new PendingPayment
                {
                    UserId = userId,
                    CartJson = System.Text.Json.JsonSerializer.Serialize(cart),
                    CustomerName = model.CustomerName,
                    DeliveryAddress = model.DeliveryAddress,
                    PhoneNumber = model.PhoneNumber,
                    Amount = total
                };

                _context.PendingPayments.Add(pending);
                await _context.SaveChangesAsync();

                // Redirect to payment controller which creates session
                return RedirectToAction("CreateSessionForPending", "Payment", new { pendingId = pending.Id });
            }

            // Cash on delivery flow (unchanged) — create order immediately
            Order order = new Order
            {
                UserId = userId,
                TotalAmount = total,
                DeliveryAddress = model.DeliveryAddress,
                PhoneNumber = model.PhoneNumber,
                CustomerName = model.CustomerName,
                Status = "Pending",
                OrderDate = DateTime.Now
            };

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); // Order needs Id for FK

                foreach (var item in cart)
                {
                    if (products.ContainsKey(item.ProductId))
                    {
                        var orderItem = new OrderItem
                        {
                            OrderId = order.Id,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            UnitPrice = products[item.ProductId].Price
                        };
                        _context.OrderItems.Add(orderItem);
                    }
                }

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = userId,
                    Action = "CreateOrder",
                    Entity = "Order",
                    EntityId = order.Id,
                    Details = $"Order created with {cart.Count} items, Total {total:C}"
                });

                await _context.SaveChangesAsync(); // persist order items + audit
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to create order for user {UserId}", userId);
                TempData["ErrorMessage"] = "An error occurred while placing your order. Please try again.";
                return RedirectToAction("Checkout");
            }

            HttpContext.Session.Remove("Cart");

            // Send confirmation email (non-blocking for DB)
            try
            {
                var email = user?.Email;
                if (!string.IsNullOrEmpty(email))
                {
                    await _emailService.SendOrderConfirmationAsync(email, model.CustomerName, order.Id, total);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send confirmation email for order {OrderId}", order.Id);
            }

            return RedirectToAction("OrderConfirmation", new { id = order.Id });
        }

        [Authorize]
        public async Task<IActionResult> OrderConfirmation(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier));

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        [Authorize]
        public async Task<IActionResult> OrderHistory()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        [Authorize]
        public async Task<IActionResult> OrderDetails(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            // Only allow cancellation if order is still pending
            if (order.Status == "Pending")
            {
                order.Status = "Cancelled";
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Order cancelled successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Order cannot be cancelled as it is already being processed.";
            }

            return RedirectToAction("OrderDetails", new { id = order.Id });
        }
    }
}