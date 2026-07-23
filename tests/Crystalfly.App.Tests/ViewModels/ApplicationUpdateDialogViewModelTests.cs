using Crystalfly.App.ViewModels.Dialogs;

namespace Crystalfly.App.Tests.ViewModels;

public sealed class ApplicationUpdateDialogViewModelTests
{
    [Theory]
    [InlineData("UpdateCommand", ApplicationUpdateDialogResult.Update)]
    [InlineData("LaterCommand", ApplicationUpdateDialogResult.Later)]
    [InlineData("SkipCommand", ApplicationUpdateDialogResult.SkipVersion)]
    public void Commands_close_with_the_selected_result(
        string commandProperty,
        ApplicationUpdateDialogResult expected)
    {
        var viewModel = new ApplicationUpdateDialogViewModel(
            "Update available",
            "0.6.1",
            "notes",
            "Update",
            "Later",
            "Skip");
        object? result = null;
        viewModel.RequestClose += (_, value) => result = value;

        var command = Assert.IsAssignableFrom<System.Windows.Input.ICommand>(
            typeof(ApplicationUpdateDialogViewModel).GetProperty(commandProperty)!.GetValue(viewModel));
        command.Execute(null);

        Assert.Equal(expected, result);
    }
}
