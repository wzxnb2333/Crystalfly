namespace Crystalfly.App.Updates;

public enum ApplicationUpdateProgressStage
{
    Downloading,
    Verifying,
    StartingUpdater
}

public sealed record ApplicationUpdateProgress(
    ApplicationUpdateProgressStage Stage,
    long BytesReceived,
    long TotalBytes);
