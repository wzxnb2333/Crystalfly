namespace Crystalfly.Updater;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            return await UpdaterApplication.RunAsync(
                args,
                ParentProcessWaiter.WaitForExitAsync,
                UpdateAssetVerifier.VerifyAndLockAsync,
                InstalledUpdateInstaller.RunAsync,
                async (asset, target, cancellationToken) =>
                    await PortableUpdateInstaller.ApplyAsync(asset, target, cancellationToken).ConfigureAwait(false),
                UpdaterApplication.Restart,
                PortableUpdateInstaller.WaitForHealthAndCompleteAsync,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidDataException
            or IOException
            or UnauthorizedAccessException
            or TimeoutException
            or InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
