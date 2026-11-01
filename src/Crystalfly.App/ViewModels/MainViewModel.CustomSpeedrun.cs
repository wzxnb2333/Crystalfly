using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;
using Crystalfly.Core.Runtime;

namespace Crystalfly.App.ViewModels;

public partial class MainViewModel
{
    public string SpeedrunEnvironmentTitle => Loc[IsSilksong ? "SpeedrunCustomTitle" : "SpeedrunTitle"];
    public string SpeedrunEnvironmentHint => Loc[IsSilksong ? "SpeedrunCustomHint" : "SpeedrunHint"];
    public string SpeedrunSourcePlaceholder => Loc[IsSilksong ? "SpeedrunSelectSource" : "SpeedrunSelectVanillaSource"];
    public string SpeedrunLaunchLabel => Loc[IsSilksong ? "LaunchGame" : "SpeedrunVerifyAndLaunch"];
    public bool HasRuntimePatchesEnvironment => IsHollowKnight && HasSelectedSpeedrunInstance;

    private void NotifySpeedrunLabels()
    {
        OnPropertyChanged(nameof(SpeedrunEnvironmentTitle));
        OnPropertyChanged(nameof(SpeedrunEnvironmentHint));
        OnPropertyChanged(nameof(SpeedrunSourcePlaceholder));
        OnPropertyChanged(nameof(SpeedrunLaunchLabel));
        OnPropertyChanged(nameof(HasRuntimePatchesEnvironment));
    }

    private async Task CreateSilksongSpeedrunInstanceAsync()
    {
        if (IsMutationBlocked()) return;
        if (SelectedSpeedrunSourceInstance?.Record is not { GameId: GameId.Silksong, Purpose: InstancePurpose.General } source
            || !SpeedrunSourceInstances.Any(item => item.Id == source.Id))
        {
            ErrorMessage = Loc["SpeedrunSelectSource"];
            return;
        }

        var name = string.IsNullOrWhiteSpace(SpeedrunEnvironmentName)
            ? UniqueInstanceName(string.Format(Loc["SpeedrunDefaultNameFormat"], SelectedGameName))
            : SpeedrunEnvironmentName.Trim();
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            InstanceRecord? clone = null;
            await instanceOperationCoordinator.RunAsync("transactions", async token =>
            {
                if (new SystemHollowKnightProcessProbe(GameDefinition.Silksong.ProcessName).IsRunning())
                    throw new InvalidOperationException(Loc["CloseGameFirst"]);
                await EnsureTransactionsHealthyAsync(token);
                clone = await Task.Run(() => InstanceCloneService.CloneAsync(
                    source.RootPath, name, Guid.NewGuid().ToString("N"), token,
                    shareContent: true, customSpeedrun: true), token);
            }, lifetimeCancellation.Token);
            SpeedrunEnvironmentName = string.Empty;
            await RefreshInstancesAsync(showBusy: false);
            SelectedSpeedrunInstance = Instances.SpeedrunInstances.FirstOrDefault(item => item.Id == clone!.Id);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException
            or InvalidOperationException or UnauthorizedAccessException or ArgumentException or OperationCanceledException)
        {
            if (!lifetimeCancellation.IsCancellationRequested)
                ErrorMessage = Loc.ErrorMessageFor(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
