using System.Collections.Generic;
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

        // --------------------------------------------
        // Email + SMS only
        // --------------------------------------------
        public async Task SendAlertAsync(
            string? email,
            string? phone,
            string message)
        {
            var tasks = new List<Task>();

            if (!string.IsNullOrWhiteSpace(email))
                tasks.Add(_email.SendAlertAsync(email, message));

            if (!string.IsNullOrWhiteSpace(phone))
                tasks.Add(_sms.SendAlertAsync(phone, message));

            if (tasks.Count == 0)
                return;

            await Task.WhenAll(tasks);
        }
    }
}
