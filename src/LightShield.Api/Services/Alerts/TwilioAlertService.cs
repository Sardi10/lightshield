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
            try
            {
                TwilioClient.Init(_accountSid, _authToken);

                await MessageResource.CreateAsync(
                    body: message,
                    from: new Twilio.Types.PhoneNumber(_fromNumber),
                    to: new Twilio.Types.PhoneNumber(toPhone)
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Twilio] Failed to send SMS: {ex.Message}");
            }
        }
    }
}
