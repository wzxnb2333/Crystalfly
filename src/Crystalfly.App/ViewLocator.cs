using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Crystalfly.App.ViewModels;

namespace Crystalfly.App;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        var localization = (App.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow?.DataContext is MainViewModel viewModel
            ? viewModel.Loc
            : null;
        return new TextBlock
        {
            Text = localization is null
                ? $"Not Found: {name}"
                : string.Format(System.Globalization.CultureInfo.CurrentUICulture, localization["ViewNotFound"], name),
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
