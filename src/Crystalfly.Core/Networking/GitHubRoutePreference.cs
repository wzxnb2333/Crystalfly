using Crystalfly.Core.Configuration;

namespace Crystalfly.Core.Networking;

public sealed class GitHubRoutePreference
{
    private int preferredRoute = (int)GitHubDownloadRoute.GhProxyOrg;

    public GitHubDownloadRoute PreferredRoute => (GitHubDownloadRoute)Volatile.Read(ref preferredRoute);

    public void SetPreferred(GitHubDownloadRoute route)
    {
        if (route is GitHubDownloadRoute.Direct
            or GitHubDownloadRoute.Mirror
            or GitHubDownloadRoute.GhProxyOrg
            or GitHubDownloadRoute.GhProxyNet
            or GitHubDownloadRoute.GhFastTop)
        {
            Volatile.Write(ref preferredRoute, (int)route);
        }
    }

    public void UpdateFromLatency(IEnumerable<GitHubRouteLatencyResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        GitHubRouteLatencyResult? fastest = results
            .Where(result => result.Status == GitHubRouteLatencyStatus.Success && result.Latency is not null)
            .OrderBy(result => result.Latency)
            .FirstOrDefault();
        if (fastest is not null)
        {
            SetPreferred(fastest.Route);
        }
    }
}
