namespace Crystalfly.Core.Networking;

public sealed class OfflineModeException : HttpRequestException
{
    public OfflineModeException()
        : base("Network access is disabled while offline mode is enabled.")
    {
    }
}

public sealed class OfflineTransitionException : OperationCanceledException
{
    public OfflineTransitionException(Exception innerException, CancellationToken cancellationToken)
        : base("Network work was canceled because offline mode was enabled.", innerException, cancellationToken)
    {
    }
}
