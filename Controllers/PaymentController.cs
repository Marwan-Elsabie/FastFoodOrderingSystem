using FastFoodOrderingSystem.Data;
using FastFoodOrderingSystem.Models;
using FastFoodOrderingSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;
using System.Text;
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

        public PaymentController(IPaymentService paymentService,
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<PaymentController> logger,
            IEmailService emailService)
        {
            _paymentService = paymentService;
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _emailService = emailService;
            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
        }

        // GET: /Payment/CreateSessionForPending/{pendingId}
        [HttpGet]
        public async Task<IActionResult> CreateSessionForPending(int pendingId)
        {
            var pending = await _context.PendingPayments.FindAsync(pendingId);
            if (pending == null) return NotFound();

            // Create Checkout session with metadata referencing the pending id
            var domain = $"{Request.Scheme}://{Request.Host}";
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
                            Currency = "usd",
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
                    { "pendingPaymentId", pending.Id.ToString() }
                }
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            // Redirect to Stripe Checkout
            if (!string.IsNullOrEmpty(session.Url))
            {
                return Redirect(session.Url);
            }

            // Fallback if session URL is empty
            TempData["ErrorMessage"] = "Unable to create Stripe checkout session. Please try again.";
            return RedirectToAction("Checkout", "Cart");
        }

        // GET: /Payment/StripeSuccess
        [HttpGet]
        public IActionResult StripeSuccess(string session_id)
        {
            // The webhook will create the order. Inform user we are processing and will email confirmation.
            TempData["SuccessMessage"] = "Payment completed. We are finalizing your order — you will receive a confirmation email shortly.";
            return RedirectToAction("OrderHistory", "Cart");
        }

        // Stripe webhook endpoint - set this URL in your Stripe dashboard
        [HttpPost]
        [Route("api/payment/webhook")]
        public async Task<IActionResult> Webhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
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
                    if (metadata != null && metadata.TryGetValue("pendingPaymentId", out var pendingIdStr) && int.TryParse(pendingIdStr, out var pendingId))
                    {
                        var pending = await _context.PendingPayments.FindAsync(pendingId);
                        if (pending != null)
                        {
                            // Rehydrate cart
                            var cart = JsonSerializer.Deserialize<List<ShoppingCartItem>>(pending.CartJson) ?? new List<ShoppingCartItem>();

                            // Use a transaction to create order and items
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
                                    Status = "Processing",
                                    OrderDate = DateTime.Now
                                };
                                _context.Orders.Add(order);
                                await _context.SaveChangesAsync();

                                // Load products
                                var productIds = cart.Select(i => i.ProductId).ToList();
                                var products = await _context.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p);

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

                                // Remove pending
                                _context.PendingPayments.Remove(pending);

                                await _context.SaveChangesAsync();
                                await tx.CommitAsync();

                                // Send confirmation email (best-effort)
                                try
                                {
                                    var userEmail = string.Empty;
                                    if (!string.IsNullOrEmpty(pending.UserId))
                                    {
                                        var user = await _context.Users.FindAsync(pending.UserId);
                                        userEmail = user?.Email ?? string.Empty;
                                    }

                                    if (!string.IsNullOrEmpty(userEmail))
                                    {
                                        await _emailService.SendOrderConfirmationAsync(userEmail, pending.CustomerName, order.Id, order.TotalAmount);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error sending order confirmation after Stripe webhook for order {OrderId}", order.Id);
                                }
                            }
                            catch (Exception ex)
                            {
                                await tx.RollbackAsync();
                                _logger.LogError(ex, "Failed to create order from pending payment {PendingId}", pending.Id);
                            }
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
        }
    }
}