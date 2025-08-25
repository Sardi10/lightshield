using LightShield.Api.Data;
using Microsoft.EntityFrameworkCore;
using LightShield.Api.Models;


namespace LightShield.Api.Services
{
    /// <summary>
    /// Provides access to anomaly detection thresholds stored in UserConfiguration.
    /// </summary>
    public class ConfigurationService
    {
        private readonly EventsDbContext _db;

        public ConfigurationService(EventsDbContext db)
        {
            _db = db;
        }

        private async Task<UserConfiguration?> GetConfigAsync()
        {
            return await _db.Set<UserConfiguration>().FirstOrDefaultAsync();
        }

        public async Task<int> GetFailedLoginThresholdAsync()
        {
            var config = await GetConfigAsync();
            return config?.MaxFailedLogins ?? 15;
        }

        public async Task<int> GetFileDeleteThresholdAsync()
        {
            var config = await GetConfigAsync();
            return config?.MaxFileDeletes ?? 20;
        }

        public async Task<int> GetFileCreateThresholdAsync()
        {
            var config = await GetConfigAsync();
            return config?.MaxFileCreates ?? 75;
        }

        public async Task<int> GetFileEditThresholdAsync()
        {
            var config = await GetConfigAsync();
            return config?.MaxFileModifies ?? 100;
        }
    }
}
