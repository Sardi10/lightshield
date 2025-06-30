using System;
using System.Threading.Tasks;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Microsoft.Extensions.Configuration;

namespace LightShield.Api.Services.Alerts
{
    public class TwilioAlertService : IAlertService
    {
        private readonly string _accountSid;
        private readonly string _authToken;
        private readonly string _fromNumber;
        private readonly string _toNumber;

        public TwilioAlertService(IConfiguration config)
        {
            _accountSid = config["TWILIO_SID"];
            _authToken = config["TWILIO_TOKEN"];
            _fromNumber = config["TWILIO_FROM"];
            _toNumber = config["ALERT_PHONE"];
        }

        public async Task SendAlertAsync(string message)
        {
            try
            {
                TwilioClient.Init(_accountSid, _authToken);

                var msg = await MessageResource.CreateAsync(
                    body: message,
                    from: new Twilio.Types.PhoneNumber(_fromNumber),
                    to: new Twilio.Types.PhoneNumber(_toNumber)
                );

                Console.WriteLine($"[Twilio] Alert sent: SID={msg.Sid}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Twilio] Failed to send alert: {ex.Message}");
            }
        }
    }
}
