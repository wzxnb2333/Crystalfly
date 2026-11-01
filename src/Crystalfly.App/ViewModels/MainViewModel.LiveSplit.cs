using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Crystalfly.Core.Configuration;
using Crystalfly.Core.Instances;
using Crystalfly.Core.Models;

namespace Crystalfly.App.ViewModels;

public partial class MainViewModel
{
    private readonly Action<string> openLiveSplitFile;

    public string? SpeedrunLiveSplitPath => SelectedSpeedrunInstance?.Record.SpeedrunLiveSplitPath;
    public bool HasSpeedrunLiveSplitBinding => !string.IsNullOrWhiteSpace(SpeedrunLiveSplitPath);
    public string SpeedrunLiveSplitName => HasSpeedrunLiveSplitBinding
        ? Path.GetFileName(SpeedrunLiveSplitPath)! : Loc["SpeedrunLiveSplitChoose"];

    internal Task BindSpeedrunLiveSplitAsync(InstanceRecord target, string? path)
    {
        if (target.Purpose == InstancePurpose.General || target.GameId != SelectedGameId) return Task.CompletedTask;
        string? normalized = null;
        if (path is not null)
        {
            if (!LiveSplitFavoritePathPolicy.TryNormalizeExistingFile(path, out var validPath))
            {
                ErrorMessage = Loc["LiveSplitFavoriteInvalid"];
                return Task.CompletedTask;
            }
            normalized = validPath;
        }
        return RunInstanceMutationAsync(async record =>
        {
            var current = await InstanceSidecar.LoadAsync(record.RootPath, lifetimeCancellation.Token);
            if (current is null || current.Id != record.Id || current.GameId != record.GameId
                || current.Purpose == InstancePurpose.General)
                throw new InvalidDataException("The speedrun instance identity changed.");
            await InstanceSidecar.SaveAsync(current with { SpeedrunLiveSplitPath = normalized }, lifetimeCancellation.Token);
        }, target);
    }

    [RelayCommand]
    private Task UnbindSpeedrunLiveSplitAsync() => SelectedSpeedrunInstance is { } instance
        ? BindSpeedrunLiveSplitAsync(instance.Record, null) : Task.CompletedTask;

    private void OpenBoundLiveSplit(InstanceRecord instance)
    {
        if (instance.Purpose == InstancePurpose.General || string.IsNullOrWhiteSpace(instance.SpeedrunLiveSplitPath)) return;
        try
        {
            if (!LiveSplitFavoritePathPolicy.TryNormalizeExistingFile(instance.SpeedrunLiveSplitPath, out var path))
                throw new FileNotFoundException(Loc["LiveSplitFavoriteMissing"]);
            openLiveSplitFile(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidOperationException or Win32Exception or ArgumentException or NotSupportedException)
        {
            ErrorMessage = string.Format(Loc.Culture, Loc["SpeedrunLiveSplitOpenFailed"], Loc.ErrorMessageFor(exception));
        }
    }
}
