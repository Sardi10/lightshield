using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace LightShield.Api.Services.Alerts
{
    public class SmtpAlertService : IAlertService
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _user;
        private readonly string _pass;
        private readonly string _from;
        private readonly string _to;

        public SmtpAlertService(IConfiguration config)
        {
            _host = config["SMTP_HOST"];
            _port = int.Parse(config["SMTP_PORT"] ?? "25");
            _user = config["SMTP_USER"];
            _pass = config["SMTP_PASS"];
            _from = config["ALERT_EMAIL_FROM"] ?? _user;
            _to = config["ALERT_EMAIL_TO"];
        }

        public async Task SendAlertAsync(string message)
        {
            using var client = new SmtpClient(_host, _port)
            {
                Credentials = new NetworkCredential(_user, _pass),
                EnableSsl = true
            };

            var mail = new MailMessage(_from, _to)
            {
                Subject = "[LightShield Alert]",
                Body = message
            };

            await client.SendMailAsync(mail);
        }
    }
}
