using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LightShield.Api.Data;
using LightShield.Api.Models;
using LightShield.Api.Services.Alerts;

namespace LightShield.Api.Services
{
    public class FileBaselineService
    {
        private readonly EventsDbContext _db;

        public FileBaselineService(EventsDbContext db)
        {
            _db = db;
        }

        public async Task UpdateBaselinesAsync(CancellationToken token)
        {
            // Learn from past behavior ONLY
            var baselineWindowStart = DateTime.UtcNow.AddHours(-6);
            var baselineWindowEnd = DateTime.UtcNow.AddMinutes(-5);

            var events = await _db.Events
                .Where(e =>
                    e.Timestamp >= baselineWindowStart &&
                    e.Timestamp <= baselineWindowEnd &&
                    (e.Type == "filecreate" ||
                     e.Type == "filemodify" ||
                     e.Type == "filedelete" ||
                     e.Type == "filerename"))
                .ToListAsync(token);

            var groupedByHost = events.GroupBy(e => e.Hostname);

            foreach (var hostGroup in groupedByHost)
            {
                var hostname = hostGroup.Key;

                double minutes = (baselineWindowEnd - baselineWindowStart).TotalMinutes;
                if (minutes <= 0) continue;

                double createRate = hostGroup.Count(e => e.Type == "filecreate") / minutes;
                double modifyRate = hostGroup.Count(e => e.Type == "filemodify") / minutes;
                double deleteRate = hostGroup.Count(e => e.Type == "filedelete") / minutes;
                double renameRate = hostGroup.Count(e => e.Type == "filerename") / minutes;

                var baseline = await _db.FileActivityBaselines
                    .FirstOrDefaultAsync(b => b.Hostname == hostname, token);

                if (baseline == null)
                {
                    baseline = new FileActivityBaseline
                    {
                        Hostname = hostname
                    };
                    _db.FileActivityBaselines.Add(baseline);
                }

                // Simple exponential smoothing (stable + explainable)
                const double alpha = 0.3;

                baseline.CreateAvg = Smooth(baseline.CreateAvg, createRate, alpha);
                baseline.ModifyAvg = Smooth(baseline.ModifyAvg, modifyRate, alpha);
                baseline.DeleteAvg = Smooth(baseline.DeleteAvg, deleteRate, alpha);
                baseline.RenameAvg = Smooth(baseline.RenameAvg, renameRate, alpha);

                baseline.CreateStd = UpdateStd(baseline.CreateStd, createRate, baseline.CreateAvg);
                baseline.ModifyStd = UpdateStd(baseline.ModifyStd, modifyRate, baseline.ModifyAvg);
                baseline.DeleteStd = UpdateStd(baseline.DeleteStd, deleteRate, baseline.DeleteAvg);
                baseline.RenameStd = UpdateStd(baseline.RenameStd, renameRate, baseline.RenameAvg);

                baseline.LastUpdated = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(token);
        }

        private static double Smooth(double oldValue, double newValue, double alpha)
        {
            if (oldValue == 0)
                return newValue;

            return alpha * newValue + (1 - alpha) * oldValue;
        }

        private static double UpdateStd(double oldStd, double value, double mean)
        {
            var diff = value - mean;
            var variance = diff * diff;
            return Math.Sqrt((oldStd * oldStd + variance) / 2);
        }
    }
}
