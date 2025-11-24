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
            // -----------------------------
            // GMAIL SMTP (Active)
            // -----------------------------
            _host = config["SMTP_HOST"] ?? "smtp.gmail.com";
            _port = int.Parse(config["SMTP_PORT"] ?? "587");
            _user = config["SMTP_USER"];      // your gmail
            _pass = config["SMTP_PASS"];      // gmail app password
            _from = config["ALERT_EMAIL_FROM"];
            _to = config["ALERT_EMAIL_TO"];

            // -----------------------------
            // SENDGRID SMTP 
            // -----------------------------
            
            //
            // _host = "smtp.sendgrid.net";
            // _port = 587;
            // _user = "apikey";
            // _pass = config["SENDGRID_API_KEY"];
            // _from = config["ALERT_EMAIL_FROM"];
            // _to = config["ALERT_EMAIL_TO"];
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

            Console.WriteLine("[Email] Alert email sent via Gmail SMTP.");
        }
    }
}
