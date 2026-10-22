using System.Reflection;
using System.Text.RegularExpressions;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class LocalizationViewModelTests
{

    [Fact]
    public void English_and_chinese_dictionaries_have_matching_keys_and_placeholders()
    {
        var type = typeof(LocalizationViewModel);
        var english = (IReadOnlyDictionary<string, string>)type
            .GetField("English", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        var chinese = (IReadOnlyDictionary<string, string>)type
            .GetField("Chinese", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        Assert.Equal(english.Keys.OrderBy(value => value), chinese.Keys.OrderBy(value => value));
        Assert.All(english, pair =>
        {
            Assert.False(string.IsNullOrWhiteSpace(pair.Value), pair.Key);
            Assert.False(string.IsNullOrWhiteSpace(chinese[pair.Key]), pair.Key);
            Assert.Equal(Placeholders(pair.Value), Placeholders(chinese[pair.Key]));
        });
    }

    private static IReadOnlyList<string> Placeholders(string value) =>
        Regex.Matches(value, @"\\{\\d+(?:[^}]*)\\}")
            .Select(match => match.Value)
            .OrderBy(value => value)
            .ToArray();
}
