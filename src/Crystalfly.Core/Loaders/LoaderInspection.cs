using Crystalfly.Core.Models;

namespace Crystalfly.Core.Loaders;

public enum LoaderOwnership
{
    None,
    Managed,
    External
}

public sealed record LoaderInspection
{
    public required LoaderState State { get; init; }

    public string? PackageId { get; init; }

    public string? Version { get; init; }

    public bool IsVerified { get; init; }

    public LoaderOwnership Ownership { get; init; }
}
