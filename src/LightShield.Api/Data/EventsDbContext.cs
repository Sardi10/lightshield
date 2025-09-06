using System;
using Microsoft.EntityFrameworkCore;
using LightShield.Api.Models;

namespace LightShield.Api.Data
{
    public class EventsDbContext : DbContext
    {
        public EventsDbContext(DbContextOptions<EventsDbContext> options)
            : base(options) { }

        public DbSet<Event> Events { get; set; } = default!;

        public DbSet<Anomaly> Anomalies { get; set; }

        public DbSet<Alert> Alerts { get; set; }

        public DbSet<UserConfiguration> Configurations { get; set; } = null!;
    }
}
