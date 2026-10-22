using System.Text.Json;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Diagnostics;

namespace Crystalfly.Core.Tests.Diagnostics;

public sealed class ErrorReportTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Crystalfly-Diagnostics-" + Guid.NewGuid());

    [Fact]
    public void Report_uses_metadata_only_even_for_nested_sensitive_exceptions()
    {
        var report = ErrorReport.Create(SensitiveException(), ErrorOperation.SaveWrite, false, "1.2.3+abc123", DateTimeOffset.UtcNow);
        var json = JsonSerializer.Serialize(report, ErrorReport.JsonOptions);
        Assert.True(report.IsValid());
        Assert.DoesNotContain("Alice", json);
        Assert.DoesNotContain("secret", json);
        Assert.DoesNotContain("https", json);
        Assert.DoesNotContain("user1.dat", json);
        Assert.Contains("SensitiveException", json);
        Assert.Contains("System.IO.IOException", json);
        Assert.Equal(2, report.Errors.Count);
    }

    internal static Exception SensitiveException()
    {
        try { throw new IOException(@"C:\Users\Alice\user1.dat?token=secret https://example.invalid/?password=secret", new Exception("Bearer secret")); }
        catch (Exception exception) { exception.Data["token"] = "secret"; return exception; }
    }

    [Fact]
    public void Expected_cancellation_and_validation_are_not_errors()
    {
        Assert.True(ErrorReport.ShouldIgnore(new TaskCanceledException()));
        Assert.True(ErrorReport.ShouldIgnore(new ArgumentException("input")));
        Assert.True(ErrorReport.ShouldIgnore(new FormatException()));
        Assert.True(ErrorReport.ShouldIgnore(new AggregateException(new OperationCanceledException())));
        Assert.False(ErrorReport.ShouldIgnore(new IOException()));
    }

    [Fact]
    public void History_persists_deduplication_daily_limit_and_seven_day_retention()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new LocalErrorReportStore(root);
        var first = ErrorReport.Create(new IOException(), ErrorOperation.Download, false, "1.0", now);
        Assert.True(store.TryAdd(first));
        Assert.False(new LocalErrorReportStore(root).TryAdd(first with { Id = Guid.NewGuid() }));
        for (var index = 1; index < 5; index++)
            Assert.True(store.TryAdd(first with { Id = Guid.NewGuid(), Fingerprint = index.ToString("X64") }));
        Assert.False(store.TryAdd(first with { Id = Guid.NewGuid(), Fingerprint = 9.ToString("X64") }));
        Assert.Equal(5, store.Read(now).Count);
        Assert.Empty(store.Read(now.AddDays(8)));
    }

    [Fact]
    public void History_discards_corrupt_oversized_and_invalid_data_and_caps_at_twenty()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "corrupt.json"), "{");
        File.WriteAllText(Path.Combine(root, "large.json"), new string('x', ErrorReport.MaxBytes + 1));
        var store = new LocalErrorReportStore(root);
        var now = DateTimeOffset.UtcNow;
        for (var day = 0; day < 5; day++)
            for (var index = 0; index < 5; index++)
                Assert.True(store.TryAdd(ErrorReport.Create(new IOException(), (ErrorOperation)index, false, "1.0", now.AddDays(day))));
        Assert.Equal(20, store.Read(now.AddDays(4)).Count);
        Assert.Equal(20, Directory.GetFiles(root).Length);
        Assert.False(store.TryAdd(ErrorReport.Create(new IOException(), (ErrorOperation)999, false, "1.0", now.AddDays(5))));
    }

    [Fact]
    public async Task Settings_default_on_and_explicit_off_survive_save_and_upgrade()
    {
        var path = Path.Combine(root, "settings.json");
        Assert.True((await CrystalflySettingsStore.LoadAsync(path)).ErrorReportingEnabled);
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(path, "{}");
        Assert.True((await CrystalflySettingsStore.LoadAsync(path)).ErrorReportingEnabled);
        await CrystalflySettingsStore.SaveAsync(path, new CrystalflySettings { ErrorReportingEnabled = false, ErrorReportingNoticeVersion = 1 });
        var settings = await CrystalflySettingsStore.LoadAsync(path);
        Assert.False(settings.ErrorReportingEnabled);
        Assert.Equal(1, settings.ErrorReportingNoticeVersion);
        await CrystalflySettingsStore.SaveAsync(path, settings with { OfflineMode = true });
        Assert.False((await CrystalflySettingsStore.LoadAsync(path)).ErrorReportingEnabled);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
