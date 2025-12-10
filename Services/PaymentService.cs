using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;
using System.Text.Json;

namespace FastFoodOrderingSystem.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PaymentService(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
        }

        // Create a Stripe Checkout session and return the session URL
        public async Task<PaymentSessionResult> CreatePaymentSessionAsync(decimal amount, string currency = "usd")
        {
            // Convert to cents (Stripe expects smallest currency unit)
            var amountInCents = (long)(Math.Round(amount, 2) * 100m);

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
                            UnitAmount = amountInCents,
                            Currency = currency,
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "Order payment"
                            }
                        }
                    }
                },
                // These will be overridden by the caller with appropriate URLs
                SuccessUrl = $"{GetBaseUrl()}/Payment/StripeSuccess?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{GetBaseUrl()}/Cart/Checkout"
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            return new PaymentSessionResult(session.Id, session.Url);
        }

        private string GetBaseUrl()
        {
            var req = _httpContextAccessor.HttpContext?.Request;
            if (req == null) return _configuration["AppBaseUrl"] ?? "https://localhost:5001";
            return $"{req.Scheme}://{req.Host}";
        }
    }
}