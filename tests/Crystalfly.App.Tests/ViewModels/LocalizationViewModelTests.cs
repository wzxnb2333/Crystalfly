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
        { "LatencyCanceled", "Canceled", "已取消" }
    };

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
