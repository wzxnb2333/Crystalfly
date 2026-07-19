namespace Crystalfly.Core.Packages;

public sealed record PackageTransferProgress(
    long CompletedBytes,
    long TotalBytes,
    double BytesPerSecond,
    string Stage);
