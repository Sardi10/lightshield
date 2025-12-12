using System;
using System.Threading.Tasks;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Microsoft.Extensions.Configuration;

namespace LightShield.Api.Services.Alerts
{
    public class TwilioAlertService
    {
        private readonly string _accountSid;
        private readonly string _authToken;
        private readonly string _fromNumber;

        public TwilioAlertService(IConfiguration config)
        {
            _accountSid = config["TWILIO_SID"];
            _authToken = config["TWILIO_TOKEN"];
            _fromNumber = config["TWILIO_FROM"];
        }

        public async Task SendAlertAsync(string toPhone, string message)
        {
            // Do not even attempt sending if SMS is not configured
            if (string.IsNullOrWhiteSpace(_accountSid) ||
                string.IsNullOrWhiteSpace(_authToken) ||
                string.IsNullOrWhiteSpace(_fromNumber))
            {
                Console.WriteLine("[Twilio] SMS sending skipped: Twilio credentials missing.");
                return;
            }

            if (string.IsNullOrWhiteSpace(toPhone))
            {
                Console.WriteLine("[Twilio] SMS sending skipped: No recipient phone.");
                return;
            }

            try
            {
                TwilioClient.Init(_accountSid, _authToken);

                var msg = await MessageResource.CreateAsync(
                    body: message,
                    from: new Twilio.Types.PhoneNumber(_fromNumber),
                    to: new Twilio.Types.PhoneNumber(toPhone)
                );

                Console.WriteLine($"[Twilio] SMS sent. SID={msg.Sid}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Twilio] Failed to send SMS: {ex.Message}");
            }
        }
    }
}
