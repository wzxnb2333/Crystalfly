using System.Text.Json;
using Crystalfly.Core.Serialization;

namespace Crystalfly.Core.Diagnostics;

/// <summary>Bounded local history for inspection/export and rate limiting; not a delivery queue.</summary>
public sealed class LocalErrorReportStore(string directory)
{
    private readonly object gate = new();

    public bool TryAdd(ErrorReport report)
    {
        lock (gate)
        {
            var recent = Read(report.Timestamp);
            var today = recent.Count(item => item.Timestamp.UtcDateTime.Date == report.Timestamp.UtcDateTime.Date);
            if (today >= 5 || recent.Any(item => item.Release == report.Release
                && item.Fingerprint == report.Fingerprint
                && report.Timestamp - item.Timestamp < TimeSpan.FromDays(1))) return false;
            var bytes = JsonSerializer.SerializeToUtf8Bytes(report, ErrorReport.JsonOptions);
            if (!report.IsValid() || bytes.Length > ErrorReport.MaxBytes) return false;
            Directory.CreateDirectory(directory);
            // Capture can run on the UI thread or during a fatal exception. Keep the atomic
            // writer off the caller's synchronization context while waiting for durable storage.
            Task.Run(() => AtomicJsonStore.WriteAsync(Path.Combine(directory, report.Id.ToString("N") + ".json"),
                report, CancellationToken.None)).GetAwaiter().GetResult();
            foreach (var old in recent.OrderByDescending(item => item.Timestamp).Skip(ErrorReport.MaxReports - 1))
            {
                File.Delete(Path.Combine(directory, old.Id.ToString("N") + ".json"));
            }
            return true;
        }
    }

    public IReadOnlyList<ErrorReport> Read(DateTimeOffset now)
    {
        lock (gate)
        {
            var reports = new List<ErrorReport>();
            foreach (var file in DiagnosticFiles.GetFiles(directory))
            {
                if (file.Extension != ".json" || file.Length > ErrorReport.MaxBytes
                    || now.UtcDateTime - file.LastWriteTimeUtc > ErrorReport.Retention)
                {
                    file.Delete();
                    continue;
                }
                try
                {
                    var item = JsonSerializer.Deserialize<ErrorReport>(File.ReadAllBytes(file.FullName), CrystalflyJson.Options);
                    if (item is not null && item.IsValid() && item.Timestamp <= now + TimeSpan.FromMinutes(5)
                        && now - item.Timestamp <= ErrorReport.Retention
                        && file.Name == item.Id.ToString("N") + ".json") reports.Add(item);
                    else file.Delete();
                }
                catch (JsonException) { file.Delete(); }
            }
            foreach (var item in reports.OrderByDescending(item => item.Timestamp).Skip(ErrorReport.MaxReports))
            {
                File.Delete(Path.Combine(directory, item.Id.ToString("N") + ".json"));
            }
            return reports.OrderByDescending(item => item.Timestamp).Take(ErrorReport.MaxReports).ToArray();
        }
    }
}
