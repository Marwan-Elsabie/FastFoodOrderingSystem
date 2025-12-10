using System.Threading.Tasks;

namespace FastFoodOrderingSystem.Services
{
    public interface IPaymentService
    {
        // Create a placeholder payment session / intent — extend to integrate Stripe/PayPal
        Task<PaymentSessionResult> CreatePaymentSessionAsync(decimal amount, string currency = "usd");
    }

    public record PaymentSessionResult(string SessionId, string RedirectUrl);
}