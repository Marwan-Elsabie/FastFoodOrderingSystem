using FastFoodOrderingSystem.Data;
using FastFoodOrderingSystem.Models;
using FastFoodOrderingSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Stripe;
using Stripe.Checkout;
using System.Security.Claims;
using System.Text.Json;

namespace FastFoodOrderingSystem.Controllers
{
    public class PaymentController : Controller
    {
        private readonly IPaymentService _paymentService;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentController> _logger;
        private readonly IEmailService _emailService;
        private readonly IWebHostEnvironment _env;

        public PaymentController(
            IPaymentService paymentService,
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<PaymentController> logger,
            IEmailService emailService,
            IWebHostEnvironment env)
        {
            _paymentService = paymentService;
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _emailService = emailService;
            _env = env;
        }

        // GET: /Payment/CreateSessionForPending/{pendingId}
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> CreateSessionForPending(int pendingId)
        {
            var pending = await _context.PendingPayments.FindAsync(pendingId);
            if (pending == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.Equals(pending.UserId, userId, StringComparison.Ordinal))
                return Forbid();

            var domain = $"{Request.Scheme}://{Request.Host}";
            var currency = _configuration["Stripe:Currency"] ?? "usd";

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                Mode = "payment",
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(Math.Round(pending.Amount, 2) * 100m),
                            Currency = currency,
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Order #{pending.Id}"
                            }
                        }
                    }
                },
                SuccessUrl = domain + $"/Payment/StripeSuccess?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = domain + "/Cart/Checkout",
                Metadata = new Dictionary<string, string>
                {
                    { "pendingPaymentId", pending.Id.ToString() },
                    { "userId", pending.UserId ?? string.Empty }
                }
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);
            if (!string.IsNullOrEmpty(session.Url))
                return Redirect(session.Url);

            TempData["ErrorMessage"] = "Unable to create Stripe checkout session. Please try again.";
            return RedirectToAction("Checkout", "Cart");
        }

        // GET: /Payment/StripeSuccess
        [HttpGet]
        public async Task<IActionResult> StripeSuccess(string session_id)
        {
            if (string.IsNullOrEmpty(session_id))
            {
                TempData["SuccessMessage"] = "Payment completed. We are finalizing your order — you will receive a confirmation email shortly.";
                return RedirectToAction("OrderHistory", "Cart");
            }

            try
            {
                var sessionService = new SessionService();
                var session = await sessionService.GetAsync(session_id);

                var metadata = session?.Metadata;
                string? pendingIdStr = null;
                string? metadataUserId = null;
                metadata?.TryGetValue("pendingPaymentId", out pendingIdStr);
                metadata?.TryGetValue("userId", out metadataUserId);

                if (!string.IsNullOrEmpty(pendingIdStr) && int.TryParse(pendingIdStr, out var pendingId))
                {
                    var pending = await _context.PendingPayments.FindAsync(pendingId);
                    if (pending != null)
                    {
                        if (pending.ProcessedAt != null)
                        {
                            HttpContext.Session.Remove("Cart");
                            TempData["SuccessMessage"] = "Payment completed. Your order has been placed. A confirmation email will be sent shortly.";

                            var order = await _context.Orders
                                .Where(o => o.UserId == pending.UserId && o.TotalAmount == pending.Amount)
                                .OrderByDescending(o => o.OrderDate)
                                .FirstOrDefaultAsync();

                            if (order != null)
                                return RedirectToAction("OrderConfirmation", "Cart", new { id = order.Id });

                            return RedirectToAction("OrderHistory", "Cart");
                        }

                        // Fallback: if Stripe marks session paid, create order here (idempotent).
                        if (string.Equals(session?.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
                        {
                            var created = await CreateOrderFromPendingAsync(pending);
                            if (created != null)
                            {
                                HttpContext.Session.Remove("Cart");
                                return RedirectToAction("OrderConfirmation", "Cart", new { id = created.Id });
                            }
                        }

                        TempData["SuccessMessage"] = "Payment completed. We are finalizing your order — you will receive a confirmation email shortly.";
                        return RedirectToAction("OrderHistory", "Cart");
                    }
                    else
                    {
                        var userIdToCheck = !string.IsNullOrEmpty(metadataUserId) ? metadataUserId : User.FindFirstValue(ClaimTypes.NameIdentifier);
                        if (!string.IsNullOrEmpty(userIdToCheck))
                        {
                            var recentOrder = await _context.Orders
                                .Where(o => o.UserId == userIdToCheck && o.OrderDate >= DateTime.UtcNow.AddMinutes(-10))
                                .OrderByDescending(o => o.OrderDate)
                                .FirstOrDefaultAsync();

                            if (recentOrder != null)
                            {
                                HttpContext.Session.Remove("Cart");
                                return RedirectToAction("OrderConfirmation", "Cart", new { id = recentOrder.Id });
                            }
                        }

                        TempData["SuccessMessage"] = "Payment completed. We are finalizing your order — you will receive a confirmation email shortly.";
                        return RedirectToAction("OrderHistory", "Cart");
                    }
                }
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Error retrieving Stripe session on success callback.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in StripeSuccess.");
            }

            TempData["SuccessMessage"] = "Payment completed. We are finalizing your order — you will receive a confirmation email shortly.";
            return RedirectToAction("OrderHistory", "Cart");
        }

        // Stripe webhook endpoint
        [HttpPost]
        [Route("api/payment/webhook")]
        public async Task<IActionResult> Webhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            _logger.LogInformation("Stripe webhook received. Body length: {Len}", json?.Length ?? 0);

            var webhookSecret = _configuration["Stripe:WebhookSecret"];

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    webhookSecret
                );

                if (stripeEvent.Type == Events.CheckoutSessionCompleted)
                {
                    var session = stripeEvent.Data.Object as Session;
                    var metadata = session?.Metadata;

                    if (metadata != null &&
                        metadata.TryGetValue("pendingPaymentId", out var pendingIdStr) &&
                        int.TryParse(pendingIdStr, out var pendingId))
                    {
                        var pending = await _context.PendingPayments.FindAsync(pendingId);
                        if (pending != null)
                        {
                            try
                            {
                                var created = await CreateOrderFromPendingAsync(pending);
                                if (created != null)
                                {
                                    _logger.LogInformation("Created order {OrderId} from webhook for pending {PendingId}", created.Id, pending.Id);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to create order from pending payment {PendingId}", pending.Id);
                            }
                        }
                        else
                        {
                            _logger.LogInformation("PendingPayment {PendingId} not found (maybe already processed).", pendingId);
                        }
                    }
                }

                return Ok();
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe webhook error");
                return BadRequest();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected webhook processing error");
                return BadRequest();
            }
        }

        // Shared idempotent routine used by webhook and success redirect fallback.
        private async Task<Order?> CreateOrderFromPendingAsync(PendingPayment pending)
        {
            if (pending == null) return null;

            if (pending.ProcessedAt != null)
            {
                _logger.LogInformation("PendingPayment {PendingId} already processed at {ProcessedAt}.", pending.Id, pending.ProcessedAt);
                return null;
            }

            pending.ProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var cart = JsonSerializer.Deserialize<List<ShoppingCartItem>>(pending.CartJson)
                       ?? new List<ShoppingCartItem>();

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = new Order
                {
                    UserId = pending.UserId,
                    TotalAmount = pending.Amount,
                    DeliveryAddress = pending.DeliveryAddress,
                    PhoneNumber = pending.PhoneNumber,
                    CustomerName = pending.CustomerName,
                    Status = "Pending",
                    OrderDate = DateTime.Now
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                var productIds = cart.Select(i => i.ProductId).ToList();
                var products = await _context.Products
                                .Where(p => productIds.Contains(p.Id))
                                .ToDictionaryAsync(p => p.Id, p => p);

                foreach (var item in cart)
                {
                    if (products.ContainsKey(item.ProductId))
                    {
                        _context.OrderItems.Add(new OrderItem
                        {
                            OrderId = order.Id,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            UnitPrice = products[item.ProductId].Price
                        });
                    }
                }

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = pending.UserId,
                    Action = "CreateOrder(Stripe)",
                    Entity = "Order",
                    EntityId = order.Id,
                    Details = $"Order created via Stripe Checkout, amount {pending.Amount:C}"
                });

                _context.PendingPayments.Remove(pending);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                try
                {
                    var email = (await _context.Users.FindAsync(pending.UserId))?.Email;
                    if (!string.IsNullOrEmpty(email))
                        await _emailService.SendOrderConfirmationAsync(email, pending.CustomerName, order.Id, order.TotalAmount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending order confirmation after creating order for pending {PendingId}", pending.Id);
                }

                return order;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Failed to create order from pending payment {PendingId}", pending.Id);
                pending.ProcessedAt = null;
                await _context.SaveChangesAsync();
                return null;
            }
        }

        // --- Development-only diagnostics ------------------------------------------------
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> DebugPending()
        {
            if (!_env.IsDevelopment()) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var pendings = await _context.PendingPayments
                .Where(p => p.UserId == userId)
                .Select(p => new { p.Id, p.Amount, p.CreatedAt, p.ProcessedAt })
                .ToListAsync();

            var recentOrders = await _context.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .Take(10)
                .Select(o => new { o.Id, o.TotalAmount, o.OrderDate, o.Status })
                .ToListAsync();

            return Json(new { pendings, recentOrders });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ForceProcessPending(int pendingId)
        {
            if (!_env.IsDevelopment()) return NotFound();

            var pending = await _context.PendingPayments.FindAsync(pendingId);
            if (pending == null) return NotFound();

            var order = await CreateOrderFromPendingAsync(pending);
            if (order == null)
                return Json(new { success = false, message = "Could not create order (check logs)." });

            HttpContext.Session.Remove("Cart");
            return Json(new { success = true, orderId = order.Id });
        }
    }
}