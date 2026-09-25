using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Crystalfly.Core.Configuration;
using Lucide.Avalonia;

namespace Crystalfly.App.ViewModels;

public sealed class SpeedrunCommunityLinkItemViewModel : ObservableObject
{
    public SpeedrunCommunityLinkItemViewModel(SpeedrunCommunityLinkDefinition definition, bool isCustom)
    {
        Definition = definition;
        IsCustom = isCustom;
    }

    public SpeedrunCommunityLinkDefinition Definition { get; private set; }
    public bool IsCustom { get; }
    public string Id => Definition.Id;
    public string Name => Definition.Name;
    public string Group => Definition.Group.ToString();
    public string Url => Definition.Url;
    public string Host => new Uri(Definition.Url).Host;
    public LucideIconKind Icon => Definition.IconKey.ToLowerInvariant() switch
    {
        "github" => LucideIconKind.GitFork,
        "discord" => LucideIconKind.MessagesSquare,
        "speedrun" => LucideIconKind.Timer,
        _ => LucideIconKind.ExternalLink
    };

    public void Update(SpeedrunCommunityLinkDefinition definition)
    {
        Definition = definition;
        OnPropertyChanged(string.Empty);
    }
}

public sealed class SpeedrunCommunityLinksViewModel : ViewModelBase
{
    private static readonly IReadOnlyList<SpeedrunCommunityLinkDefinition> BuiltIns =
    [
        new() { Id = "hk-speedrunning", Name = "HK Speedrunning", Group = SpeedrunCommunityGroup.HollowKnight, Url = "https://github.com/hk-speedrunning", IconKey = "github" },
        new() { Id = "hk-resources", Name = "HK Resources", Group = SpeedrunCommunityGroup.HollowKnight, Url = "https://github.com/hk-speedrunning/HK-Resources", IconKey = "github" },
        new() { Id = "hk-speedrun-com", Name = "Hollow Knight on Speedrun.com", Group = SpeedrunCommunityGroup.HollowKnight, Url = "https://www.speedrun.com/hollowknight", IconKey = "speedrun" },
        new() { Id = "hk-discord", Name = "Hollow Knight Discord", Group = SpeedrunCommunityGroup.HollowKnight, Url = "https://discord.com/invite/hollowknight", IconKey = "discord" },
        new() { Id = "silksong-speedrun-com", Name = "Silksong on Speedrun.com", Group = SpeedrunCommunityGroup.Silksong, Url = "https://www.speedrun.com/silksong", IconKey = "speedrun" },
        new() { Id = "silksong-discord", Name = "Silksong Speedrunning Discord", Group = SpeedrunCommunityGroup.Silksong, Url = "https://discord.gg/3JtHPsBjHD", IconKey = "discord" }
    ];

    private readonly Func<IReadOnlyList<SpeedrunCommunityLinkDefinition>, Task> saveAsync;
    private readonly SemaphoreSlim mutationGate = new(1, 1);
    public ObservableCollection<SpeedrunCommunityLinkItemViewModel> Links { get; } = [];
    public IEnumerable<SpeedrunCommunityLinkItemViewModel> CustomLinks => Links.Where(x => x.IsCustom);
    public IEnumerable<SpeedrunCommunityLinkItemViewModel> HollowKnightLinks => Links.Where(x => !x.IsCustom && x.Definition.Group == SpeedrunCommunityGroup.HollowKnight);
    public IEnumerable<SpeedrunCommunityLinkItemViewModel> SilksongLinks => Links.Where(x => !x.IsCustom && x.Definition.Group == SpeedrunCommunityGroup.Silksong);
    public IEnumerable<SpeedrunCommunityLinkItemViewModel> OtherLinks => Links.Where(x => !x.IsCustom && x.Definition.Group == SpeedrunCommunityGroup.Other);
    public bool HasCustomLinks => CustomLinks.Any();
    public bool HasOtherLinks => OtherLinks.Any();
    public bool HasHollowKnightLinks => HollowKnightLinks.Any();
    public bool HasSilksongLinks => SilksongLinks.Any();

    public SpeedrunCommunityLinksViewModel(Func<IReadOnlyList<SpeedrunCommunityLinkDefinition>, Task> saveAsync)
    {
        this.saveAsync = saveAsync;
        Load([]);
    }

    public void Load(IEnumerable<SpeedrunCommunityLinkDefinition>? customLinks)
    {
        Links.Clear();
        foreach (var definition in BuiltIns)
        {
            Links.Add(new SpeedrunCommunityLinkItemViewModel(definition, false));
        }
        foreach (var definition in SpeedrunCommunityLinkDefinition.NormalizeStoredLinks(customLinks))
        {
            if (BuiltIns.Any(x => string.Equals(x.Id, definition.Id, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Url, definition.Url, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            Links.Add(new SpeedrunCommunityLinkItemViewModel(definition, true));
        }
        NotifyCollectionsChanged();
    }

    public async Task<bool> AddAsync(SpeedrunCommunityLinkDefinition definition)
    {
        await mutationGate.WaitAsync();
        try
        {
            if (!SpeedrunCommunityLinkDefinition.TryNormalize(definition, out var normalized)
                || Links.Any(x => string.Equals(x.Id, normalized.Id, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.Url, normalized.Url, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
            await saveAsync(CustomLinks.Select(x => x.Definition).Append(normalized).ToArray());
            Links.Add(new SpeedrunCommunityLinkItemViewModel(normalized, true));
            NotifyCollectionsChanged();
            return true;
        }
        finally
        {
            mutationGate.Release();
        }
    }

    public async Task<bool> UpdateAsync(SpeedrunCommunityLinkDefinition definition)
    {
        await mutationGate.WaitAsync();
        try
        {
            if (!SpeedrunCommunityLinkDefinition.TryNormalize(definition, out var normalized)) return false;
            var existing = Links.FirstOrDefault(x => x.IsCustom && string.Equals(x.Id, normalized.Id, StringComparison.OrdinalIgnoreCase));
            if (existing is null) return false;
            if (Links.Any(x => x != existing && string.Equals(x.Url, normalized.Url, StringComparison.OrdinalIgnoreCase))) return false;
            await saveAsync(CustomLinks.Select(x => x == existing ? normalized : x.Definition).ToArray());
            existing.Update(normalized);
            NotifyCollectionsChanged();
            return true;
        }
        finally
        {
            mutationGate.Release();
        }
    }

    public async Task<bool> RemoveAsync(string id)
    {
        await mutationGate.WaitAsync();
        try
        {
            var existing = Links.FirstOrDefault(x => x.IsCustom && string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (existing is null) return false;
            await saveAsync(CustomLinks.Where(x => x != existing).Select(x => x.Definition).ToArray());
            Links.Remove(existing);
            NotifyCollectionsChanged();
            return true;
        }
        finally
        {
            mutationGate.Release();
        }
    }

    private void NotifyCollectionsChanged()
    {
        OnPropertyChanged(nameof(CustomLinks));
        OnPropertyChanged(nameof(HollowKnightLinks));
        OnPropertyChanged(nameof(SilksongLinks));
        OnPropertyChanged(nameof(OtherLinks));
        OnPropertyChanged(nameof(HasCustomLinks));
        OnPropertyChanged(nameof(HasOtherLinks));
        OnPropertyChanged(nameof(HasHollowKnightLinks));
        OnPropertyChanged(nameof(HasSilksongLinks));
    }
}
