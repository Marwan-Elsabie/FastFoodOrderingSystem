using Microsoft.AspNetCore.Identity.UI.Services;
using System.Threading.Tasks;

namespace FastFoodOrderingSystem.Services
{
    public class NullEmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // Just ignore emails, or log them if you want
            return Task.CompletedTask;
        }
    }
}
