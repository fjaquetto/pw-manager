using Moq;
using PWManager.Application.UseCases.Import;
using PWManager.Services;
using PWManager.Services.Interfaces;
using PWManager.ViewModels;

namespace PWManager.UnitTests;

public class ImportVaultViewModelTests
{
    [Fact]
    public async Task Import_WithAValidBackup_ShowsTheSummaryAndRaisesImported()
    {
        var service = new Mock<IVaultImportService>();
        service.Setup(import => import.ImportAsync("/tmp/backup.db", "source-password"))
            .ReturnsAsync(new VaultImportResult(VaultImportStatus.Success, Added: 2, Updated: 1, Conflicts: 1));
        var picker = new Mock<IFilePickerService>();
        picker.Setup(picker => picker.PickOpenFileAsync(It.IsAny<string>()))
            .ReturnsAsync(FilePickerResult.Selected("/tmp/backup.db"));
        var viewModel = new ImportVaultViewModel(service.Object, picker.Object);
        var imported = false;
        viewModel.Imported += () => imported = true;

        await viewModel.ChooseCommand.ExecuteAsync(null);
        viewModel.Password = "source-password";
        await viewModel.ConfirmCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasResult);
        Assert.True(imported);
        Assert.Contains("2 added", viewModel.ResultMessage);
        Assert.Contains("1 updated", viewModel.ResultMessage);
        Assert.Contains("1 conflicts", viewModel.ResultMessage);
    }

    [Fact]
    public async Task Import_WithTheWrongPassword_StaysOpenAndShowsTheError()
    {
        var service = new Mock<IVaultImportService>();
        service.Setup(import => import.ImportAsync("/tmp/backup.db", "wrong-password"))
            .ReturnsAsync(new VaultImportResult(VaultImportStatus.InvalidPassword));
        var picker = new Mock<IFilePickerService>();
        picker.Setup(picker => picker.PickOpenFileAsync(It.IsAny<string>()))
            .ReturnsAsync(FilePickerResult.Selected("/tmp/backup.db"));
        var viewModel = new ImportVaultViewModel(service.Object, picker.Object);

        await viewModel.ChooseCommand.ExecuteAsync(null);
        viewModel.Password = "wrong-password";
        await viewModel.ConfirmCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsOpen);
        Assert.True(viewModel.HasError);
        Assert.Contains("incorrect", viewModel.ErrorMessage);
        Assert.False(viewModel.HasResult);
    }
}
