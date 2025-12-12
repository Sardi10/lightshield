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

        public async Task SendAlertAsync(string email, string phone, string message)
        {
            var tasks = new List<Task>();

            // Send email if user provided email
            if (!string.IsNullOrWhiteSpace(email))
                tasks.Add(_email.SendAlertAsync(email, message));

            // Send SMS if user provided phone
            if (!string.IsNullOrWhiteSpace(phone))
                tasks.Add(_sms.SendAlertAsync(phone, message));

            // If no channels configured, do nothing
            if (tasks.Count == 0)
                return;

            await Task.WhenAll(tasks);
        }
    }
}
