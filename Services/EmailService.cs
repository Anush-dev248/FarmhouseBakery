
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace FarmhouseBakery.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(
            string toEmail,
            string subject,
            string body)
        {
            var email = _configuration["EmailSettings:Email"];
            var appPassword =
                _configuration["EmailSettings:AppPassword"];

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(appPassword))
            {
                throw new InvalidOperationException(
                    "Email settings are not configured.");
            }

            var message = new MimeMessage();

            message.From.Add(
                new MailboxAddress("Farmhouse Bakery", email));

            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("plain")
            {
                Text = body
            };

            using var client = new SmtpClient();

            await client.ConnectAsync(
                "smtp.gmail.com",
                587,
                SecureSocketOptions.StartTls);

            await client.AuthenticateAsync(email, appPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}