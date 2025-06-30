using System.Threading.Tasks;

namespace LightShield.Api.Services.Alerts
{
    public class CompositeAlertService : IAlertService
    {
        private readonly TwilioAlertService _sms;
        private readonly SmtpAlertService _email;

        public CompositeAlertService(
            TwilioAlertService sms,
            SmtpAlertService email)
        {
            _sms = sms;
            _email = email;
        }

        public async Task SendAlertAsync(string message)
        {
            // send both in parallel
            var smsTask = _sms.SendAlertAsync(message);
            var emailTask = _email.SendAlertAsync(message);
            await Task.WhenAll(smsTask, emailTask);
        }
    }
}
