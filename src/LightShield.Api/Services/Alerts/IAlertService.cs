using System.Threading.Tasks;

namespace LightShield.Api.Services.Alerts
{
    public interface IAlertService
    {
        Task SendAlertAsync(string message);
    }
}
