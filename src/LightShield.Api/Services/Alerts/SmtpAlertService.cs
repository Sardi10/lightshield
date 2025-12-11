using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace LightShield.Api.Services.Alerts
{
    public class SmtpAlertService
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _user;
        private readonly string _pass;
        private readonly string _from;

        public SmtpAlertService(IConfiguration config)
        {
            _host = config["SMTP_HOST"] ?? "smtp.gmail.com";
            _port = int.Parse(config["SMTP_PORT"] ?? "587");
            _user = config["SMTP_USER"];
            _pass = config["SMTP_PASS"];
            _from = config["ALERT_EMAIL_FROM"];
        }

        public async Task SendAlertAsync(string toEmail, string message)
        {
            using var client = new SmtpClient(_host, _port)
            {
                Credentials = new NetworkCredential(_user, _pass),
                EnableSsl = true
            };

            var mail = new MailMessage(_from, toEmail)
            {
                Subject = "[LightShield Alert]",
                Body = message
            };

            await client.SendMailAsync(mail);
        }
    }
}
