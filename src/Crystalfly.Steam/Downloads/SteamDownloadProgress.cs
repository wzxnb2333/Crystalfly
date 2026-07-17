namespace Crystalfly.Steam.Downloads;

public sealed record SteamDownloadProgress(
    long CompletedBytes,
    long TotalBytes,
    double Fraction,
    string CurrentFile);
