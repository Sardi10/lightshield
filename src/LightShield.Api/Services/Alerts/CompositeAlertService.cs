using System.Threading.Tasks;

namespace LightShield.Api.Services.Alerts
{
    public class CompositeAlertService : IAlertService
    {
        private readonly TwilioAlertService _sms;
        private readonly SmtpAlertService _email;

        public CompositeAlertService(TwilioAlertService sms, SmtpAlertService email)
        {
            _sms = sms;
            _email = email;
        }

        public async Task SendAlertAsync(string email, string phone, string message)
        {
            var smsTask = string.IsNullOrWhiteSpace(phone)
                ? Task.CompletedTask
                : _sms.SendAlertAsync(phone, message);

            var emailTask = string.IsNullOrWhiteSpace(email)
                ? Task.CompletedTask
                : _email.SendAlertAsync(email, message);

            await Task.WhenAll(smsTask, emailTask);
        }
    }
}
