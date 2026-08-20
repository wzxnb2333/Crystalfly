using System.Reflection;
using System.Text.RegularExpressions;
using Crystalfly.App.ViewModels;
using Crystalfly.Core.Configuration;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class LocalizationViewModelTests
{
    public static TheoryData<string, string, string> ManagementStrings => new()
    {
        { "SelectInstance", "Select instance", "选择实例" },
        { "InstanceSettings", "Instance settings", "实例设置" },
        { "DeleteInstance", "Delete instance", "删除实例" },
        { "CloneInstance", "Clone instance", "克隆实例" },
        { "CopySuffix", "Copy", "副本" },
        { "PermanentDeleteWarning", "This action permanently deletes the instance and cannot be undone.", "此操作会永久删除实例，且无法撤销。" },
        { "DeleteBlockedGameRunning", "Close Hollow Knight before deleting this instance.", "请先关闭《空洞骑士》，再删除此实例。" },
        { "DeleteBlockedDownloads", "Cancel or finish downloads for this instance before deleting it.", "请先取消或完成此实例的下载任务，再删除实例。" },
        { "DeleteBlockedTransactions", "Resolve unfinished file transactions before deleting this instance.", "请先处理未完成的文件事务，再删除此实例。" },
        { "Information", "Information", "信息" },
        { "OpenFolder", "Open folder", "打开目录" },
        { "SelectMultiple", "Select multiple", "多选" },
        { "SelectAll", "Select all", "全选" },
        { "ClearSelection", "Clear selection", "取消选择" },
        { "BatchActions", "Batch actions", "批量操作" },
        { "BatchEnable", "Enable selected", "启用所选" },
        { "BatchDisable", "Disable selected", "停用所选" },
        { "BatchUninstall", "Uninstall selected", "卸载所选" },
        { "DependencyImpact", "Dependency impact", "依赖影响" },
        { "RepairDependencies", "Repair dependencies", "修复依赖" },
        { "WillDelete", "Will delete", "将删除" },
        { "DependenciesWillBeMissing", "Dependencies will be missing", "依赖将缺失" },
        { "WillReEnable", "Will re-enable", "将重新启用" },
        { "WillDownloadAndInstall", "Will download and install", "将下载并安装" },
        { "CannotRepair", "Cannot repair", "无法修复" },
        { "Target", "Target", "目标" },
        { "Unresolved", "Unresolved", "未解决" },
        { "TestLatency", "Test latency", "测试延迟" },
        { "LatencyTesting", "Testing...", "测试中..." },
        { "LatencyTimeout", "Timeout", "超时" },
        { "LatencyUnavailable", "Unavailable", "不可用" },
        { "LatencyCanceled", "Canceled", "已取消" },
        { "ErrorDirectoryNotFound", "A directory was not found", "目录不存在" },
        { "ErrorFileNotFound", "A file was not found", "文件不存在" },
        { "ErrorFileLocked", "A file is being used by another process", "文件正被其他进程占用" },
        { "ErrorAccessDenied", "Access was denied", "拒绝访问" },
        { "ErrorNetworkRequestFailed", "The network request failed", "网络请求失败" },
        { "ErrorNetworkTimeout", "The network request timed out", "网络请求超时" },
        { "ErrorDataInvalid", "The data is invalid or malformed", "数据无效或格式损坏" },
        { "ErrorVerificationFailed", "File verification failed", "文件校验失败" }
    };

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

    [Theory]
    [MemberData(nameof(ManagementStrings))]
    public void Management_strings_are_available_in_both_languages(
        string key,
        string english,
        string chinese)
    {
        var localization = new LocalizationViewModel();

        localization.Apply(UiLanguage.English);
        Assert.Equal(english, localization[key]);

        localization.Apply(UiLanguage.SimplifiedChinese);
        Assert.Equal(chinese, localization[key]);
    }
}
