using Microsoft.Extensions.Options;
using razorpm.Models;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace razorpm.Services
{
    public class EmailService
    {
        private readonly EmailSettings _settings;

        public EmailService(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task SendAsync(string toEmail, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_settings.SenderEmail) ||
                string.IsNullOrWhiteSpace(_settings.Password))
            {
                return;
            }

            using var mail = new MailMessage();

            mail.From = new MailAddress(_settings.SenderEmail, _settings.SenderName);
            mail.To.Add(toEmail);
            mail.Subject = subject;
            mail.Body = body;
            mail.IsBodyHtml = false;

            using var smtp = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
            {
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_settings.SenderEmail, _settings.Password),
                EnableSsl = _settings.EnableSsl
            };

            await smtp.SendMailAsync(mail);
        }
    }
}