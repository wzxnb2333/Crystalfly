using Crystalfly.App.ViewModels;
using Crystalfly.App.ViewModels.Dialogs;
using Crystalfly.Core.Configuration;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class CommunityLinkDialogViewModelTests
{
    [Theory]
    [InlineData("", "https://example.com")]
    [InlineData("Community", "https://")]
    [InlineData("Community", "http://example.com")]
    [InlineData("Community", "https://user:password@example.com")]
    public async Task Invalid_input_stays_open_and_shows_inline_error(string name, string url)
    {
        var saves = 0;
        var vm = new CommunityLinkDialogViewModel(new(), _ => { saves++; return Task.FromResult(true); }) { Name = name, Url = url };
        var closed = false;
        vm.RequestClose += (_, _) => closed = true;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.False(closed);
        Assert.Equal(0, saves);
        Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
        vm.Url = "https://example.org";
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task Duplicate_and_failed_save_remain_editable_without_closing()
    {
        var failure = false;
        var vm = new CommunityLinkDialogViewModel(new(), _ => failure ? throw new IOException("disk full") : Task.FromResult(false))
        { Name = "Community", Url = "https://example.com" };
        var closed = false;
        vm.RequestClose += (_, _) => closed = true;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(vm.Loc["SpeedrunCommunityDuplicate"], vm.ErrorMessage);
        failure = true;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.False(closed);
        Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
        Assert.True(vm.CanEdit);
    }

    [Fact]
    public async Task Saving_prevents_double_submit_and_cancellation_and_preserves_identity()
    {
        var saved = new TaskCompletionSource<bool>();
        SpeedrunCommunityLinkDefinition? result = null;
        var original = Link("one", "https://example.com/old");
        var vm = new CommunityLinkDialogViewModel(new(), definition => { result = definition; return saved.Task; }, original)
        { Name = "  Updated  ", Url = " https://example.com/help#section " };
        object? closeResult = null;
        vm.RequestClose += (_, value) => closeResult = value;
        var pending = vm.SaveCommand.ExecuteAsync(null);
        Assert.False(vm.CancelCommand.CanExecute(null));
        Assert.False(vm.SaveCommand.CanExecute(null));
        vm.Close();
        Assert.Null(closeResult);
        saved.SetResult(true);
        await pending;
        Assert.Equal(true, closeResult);
        Assert.Equal(original.Id, result!.Id);
        Assert.Equal("Updated", result.Name);
        Assert.EndsWith("#section", result.Url);
    }

    [Fact]
    public async Task Failed_link_mutations_leave_visible_collection_unchanged()
    {
        var vm = new SpeedrunCommunityLinksViewModel(_ => throw new IOException("disk full"));
        var original = Link("one", "https://example.com/one");
        vm.Load([original]);
        await Assert.ThrowsAsync<IOException>(() => vm.AddAsync(Link("two", "https://example.com/two")));
        await Assert.ThrowsAsync<IOException>(() => vm.UpdateAsync(original with { Name = "Updated" }));
        await Assert.ThrowsAsync<IOException>(() => vm.RemoveAsync(original.Id));
        Assert.Equal(original, Assert.Single(vm.CustomLinks).Definition);
    }

    [Fact]
    public async Task Editing_and_removing_custom_link_preserve_curated_links_and_url_fragment()
    {
        IReadOnlyList<SpeedrunCommunityLinkDefinition> saved = [];
        var vm = new SpeedrunCommunityLinksViewModel(links => { saved = links; return Task.CompletedTask; });
        var custom = Link("one", "https://example.com/help#section");
        Assert.True(await vm.AddAsync(custom));
        Assert.Equal(custom.Url, Assert.Single(saved).Url);
        Assert.False(await vm.AddAsync(custom with { Id = "two" }));
        Assert.True(await vm.UpdateAsync(custom with { Name = "Updated" }));
        Assert.Equal("Updated", Assert.Single(vm.CustomLinks).Name);
        Assert.True(await vm.RemoveAsync(custom.Id));
        Assert.Empty(saved);
        Assert.Equal(6, vm.Links.Count);
        Assert.False(await vm.RemoveAsync(vm.Links[0].Id));
    }

    private static SpeedrunCommunityLinkDefinition Link(string id, string url) => new()
    { Id = id, Name = "Community", Url = url, Group = SpeedrunCommunityGroup.Other };

    [Fact]
    public async Task Concurrent_adds_preserve_both_links_in_persisted_snapshot()
    {
        var firstSave = new TaskCompletionSource();
        IReadOnlyList<SpeedrunCommunityLinkDefinition> saved = [];
        var calls = 0;
        var vm = new SpeedrunCommunityLinksViewModel(async links =>
        {
            if (++calls == 1) await firstSave.Task;
            saved = links;
        });
        var first = vm.AddAsync(Link("one", "https://example.com/one"));
        var second = vm.AddAsync(Link("two", "https://example.com/two"));
        firstSave.SetResult();
        Assert.All(await Task.WhenAll(first, second), Assert.True);
        Assert.Equal(new[] { "one", "two" }, saved.Select(link => link.Id));
        Assert.Equal(saved, vm.CustomLinks.Select(link => link.Definition));
    }
}
