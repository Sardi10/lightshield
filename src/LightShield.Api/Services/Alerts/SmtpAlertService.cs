using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace LightShield.Api.Services.Alerts
{
    public class SmtpAlertService
    {
        private readonly string? _host;
        private readonly int _port;
        private readonly string? _user;
        private readonly string? _pass;
        private readonly bool _smtpEnabled;

        public SmtpAlertService(IConfiguration config)
        {
            _host = config["SMTP_HOST"];
            _user = config["SMTP_USER"];
            _pass = config["SMTP_PASS"];

            _port = int.TryParse(config["SMTP_PORT"], out int p) ? p : 587;

            // SMTP is considered enabled only if ALL values exist
            _smtpEnabled =
                !string.IsNullOrWhiteSpace(_host) &&
                !string.IsNullOrWhiteSpace(_user) &&
                !string.IsNullOrWhiteSpace(_pass);

            if (!_smtpEnabled)
            {
                Console.WriteLine("[SMTP] Email alerts disabled (missing SMTP credentials).");
            }
        }

        public async Task SendAlertAsync(string toEmail, string message)
        {
            if (!_smtpEnabled)
            {
                Console.WriteLine("[SMTP] Skipping email send — SMTP not configured.");
                return;
            }

            if (string.IsNullOrWhiteSpace(toEmail))
            {
                Console.WriteLine("[SMTP] No recipient email provided — skipping.");
                return;
            }

            try
            {
                using var client = new SmtpClient(_host!, _port)
                {
                    Credentials = new NetworkCredential(_user, _pass),
                    EnableSsl = true
                };

                var mail = new MailMessage(_user!, toEmail)
                {
                    Subject = "[LightShield Alert]",
                    Body = message
                };

                await client.SendMailAsync(mail);

                Console.WriteLine("[SMTP] Email alert sent.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SMTP] Error sending email: {ex.Message}");
            }
        }
    }
}
