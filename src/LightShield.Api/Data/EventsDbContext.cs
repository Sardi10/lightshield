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
        public DbSet<UserConfiguration> UserConfiguration { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Event>()
                .Property(e => e.Timestamp)
                .HasConversion(
                    v => v.ToUniversalTime().ToString("o"),   // Save as ISO 8601
                    v => DateTime.Parse(v).ToUniversalTime()  // Read as UTC
                );

            modelBuilder.Entity<Alert>()
                .Property(a => a.Timestamp)
                .HasConversion(
                    v => v.ToUniversalTime().ToString("o"),
                    v => DateTime.Parse(v).ToUniversalTime()
                );

            modelBuilder.Entity<Anomaly>()
                .Property(a => a.Timestamp)
                .HasConversion(
                    v => v.ToUniversalTime().ToString("o"),
                    v => DateTime.Parse(v).ToUniversalTime()
                );
        }
    }
}
