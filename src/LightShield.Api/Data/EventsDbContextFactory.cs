using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LightShield.Api.Data
{
    public class EventsDbContextFactory
        : IDesignTimeDbContextFactory<EventsDbContext>
    {
        public EventsDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<EventsDbContext>();
            // Use the same connection string you have in Program.cs
            optionsBuilder.UseSqlite("Data Source=lightshield.db");

            return new EventsDbContext(optionsBuilder.Options);
        }
    }
}
