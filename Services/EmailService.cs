using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;

namespace FastFoodOrderingSystem.Services
{
    public interface IEmailService
    {
        Task SendOrderConfirmationAsync(string toEmail, string customerName, int orderId, decimal totalAmount);
        Task SendOrderStatusUpdateAsync(string toEmail, string customerName, int orderId, string status);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendOrderConfirmationAsync(string toEmail, string customerName, int orderId, decimal totalAmount)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Fast Food Express", _configuration["EmailSettings:FromEmail"]));
            message.To.Add(new MailboxAddress(customerName, toEmail));
            message.Subject = $"Order Confirmation - #{orderId}";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $@"
                <h2>Thank you for your order, {customerName}!</h2>
                <p>Your order <strong>#{orderId}</strong> has been received and is being processed.</p>
                <p><strong>Order Total:</strong> ${totalAmount:F2}</p>
                <p>You can track your order status in your account.</p>
                <hr>
                <p>Best regards,<br>Fast Food Express Team</p>
                "
            };

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_configuration["EmailSettings:SmtpServer"],
                int.Parse(_configuration["EmailSettings:SmtpPort"]),
                MailKit.Security.SecureSocketOptions.StartTls);

            await client.AuthenticateAsync(_configuration["EmailSettings:Username"],
                _configuration["EmailSettings:Password"]);

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        public async Task SendOrderStatusUpdateAsync(string toEmail, string customerName, int orderId, string status)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Fast Food Express", _configuration["EmailSettings:FromEmail"]));
            message.To.Add(new MailboxAddress(customerName, toEmail));
            message.Subject = $"Order #{orderId} Status Update";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $@"
                <h2>Order Status Update</h2>
                <p>Dear {customerName},</p>
                <p>Your order <strong>#{orderId}</strong> status has been updated to: <strong>{status}</strong></p>
                <hr>
                <p>Thank you for choosing Fast Food Express!</p>
                "
            };

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_configuration["EmailSettings:SmtpServer"],
                int.Parse(_configuration["EmailSettings:SmtpPort"]),
                MailKit.Security.SecureSocketOptions.StartTls);

            await client.AuthenticateAsync(_configuration["EmailSettings:Username"],
                _configuration["EmailSettings:Password"]);

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}