using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using LightShield.Api.Data;
using LightShield.Api.Models;

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
            // Learn from complete historical minute buckets and exclude current activity.
            var now = DateTime.UtcNow;
            var baselineWindowStart = FloorToMinute(now.AddHours(-6));
            var baselineWindowEnd = FloorToMinute(now.AddMinutes(-5));

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
            var bucketCount = (int)(baselineWindowEnd - baselineWindowStart).TotalMinutes;
            if (bucketCount < 60) return;

            foreach (var hostGroup in groupedByHost)
            {
                var hostname = hostGroup.Key;

                var createBuckets = new double[bucketCount];
                var modifyBuckets = new double[bucketCount];
                var deleteBuckets = new double[bucketCount];
                var renameBuckets = new double[bucketCount];

                foreach (var evt in hostGroup)
                {
                    var index = (int)(FloorToMinute(evt.Timestamp) - baselineWindowStart).TotalMinutes;
                    if (index < 0 || index >= bucketCount) continue;
                    switch (evt.Type)
                    {
                        case "filecreate": createBuckets[index]++; break;
                        case "filemodify": modifyBuckets[index]++; break;
                        case "filedelete": deleteBuckets[index]++; break;
                        case "filerename": renameBuckets[index]++; break;
                    }
                }

                var createStats = RobustStats(createBuckets);
                var modifyStats = RobustStats(modifyBuckets);
                var deleteStats = RobustStats(deleteBuckets);
                var renameStats = RobustStats(renameBuckets);

                var baseline = await _db.FileActivityBaselines
                    .FirstOrDefaultAsync(b => b.Hostname == hostname, token);

                if (baseline == null)
                {
                    baseline = new FileActivityBaseline
                    {
                        Hostname = hostname,
                        FirstSeen = hostGroup.Min(e => e.Timestamp),
                        LastUpdated = now,
                        DetectionEnabled = false
                    };
                    _db.FileActivityBaselines.Add(baseline);
                }

                baseline.CreateAvg = createStats.Mean;
                baseline.ModifyAvg = modifyStats.Mean;
                baseline.DeleteAvg = deleteStats.Mean;
                baseline.RenameAvg = renameStats.Mean;
                baseline.CreateStd = createStats.StdDev;
                baseline.ModifyStd = modifyStats.StdDev;
                baseline.DeleteStd = deleteStats.StdDev;
                baseline.RenameStd = renameStats.StdDev;
                baseline.LastUpdated = now;
            }

            await _db.SaveChangesAsync(token);
        }

        private static DateTime FloorToMinute(DateTime value) =>
            new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind);

        private static (double Mean, double StdDev) RobustStats(double[] values)
        {
            const double MIN_PERSISTED_STD = 0.05;
            var sorted = values.OrderBy(value => value).ToArray();
            var capIndex = Math.Clamp((int)Math.Floor((sorted.Length - 1) * 0.99), 0, sorted.Length - 1);
            var cap = sorted[capIndex];
            var winsorized = values.Select(value => Math.Min(value, cap)).ToArray();
            var mean = winsorized.Average();
            var variance = winsorized.Length > 1
                ? winsorized.Sum(value => Math.Pow(value - mean, 2)) / (winsorized.Length - 1)
                : 0;
            return (mean, Math.Max(Math.Sqrt(variance), MIN_PERSISTED_STD));
        }
    }
}
