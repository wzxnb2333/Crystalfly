using System.Text.Json;

namespace Crystalfly.Updater;

internal sealed record PortableUpdateOperation(
    string TargetDirectory,
    string RecoveryDirectory,
    string BackupDirectory,
    string OperationLogPath,
    string HealthFilePath,
    IReadOnlyList<string>? OriginalEntryNames = null,
    bool Committed = false)
{
    public string Serialize() => JsonSerializer.Serialize(this);
}
