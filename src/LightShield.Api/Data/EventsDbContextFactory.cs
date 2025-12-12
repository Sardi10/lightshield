using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.IO;

namespace LightShield.Api.Data
{
    public class EventsDbContextFactory : IDesignTimeDbContextFactory<EventsDbContext>
    {
        public EventsDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<EventsDbContext>();

            // Store DB in C:\ProgramData\LightShield\
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "LightShield");

            Directory.CreateDirectory(dataDir);

            var dbPath = Path.Combine(dataDir, "events.db");

            optionsBuilder.UseSqlite($"Data Source={dbPath}");

            return new EventsDbContext(optionsBuilder.Options);
        }
    }
}
