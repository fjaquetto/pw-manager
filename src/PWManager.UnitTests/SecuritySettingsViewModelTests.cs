using Moq;
using PWManager.Application.UseCases.Settings;
using PWManager.Application.UseCases.Maintenance;
using PWManager.Application.UseCases.Rekey;
using PWManager.Domain.Settings;
using PWManager.Infra.Configuration;
using PWManager.Services.Interfaces;
using PWManager.ViewModels;

namespace PWManager.UnitTests;

public class SecuritySettingsViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pwmanager-security-vm-" + Guid.NewGuid().ToString("N"));

    private readonly Mock<IVaultRekeyService> _rekey = new();
    private readonly Mock<IVaultMaintenanceService> _backup = new();
    private readonly Mock<IFilePickerService> _picker = new();
    private readonly AppSettingsService _settings;

    public SecuritySettingsViewModelTests()
    {
        _settings = new AppSettingsService(new AppSettingsStore(_directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private SecuritySettingsViewModel CreateViewModel() => new(_settings, _rekey.Object, _backup.Object, _picker.Object);

    [Fact]
    public void Open_LoadsTheCurrentSettings()
    {
        _settings.Update(new AppSettings { AutoLockMinutes = 5, LockWhenMinimized = true, ClipboardClearSeconds = 0, HidePasswordsAfterSeconds = 45 });
        var viewModel = CreateViewModel();

        viewModel.Open();

        Assert.Equal(5, viewModel.AutoLockMinutes);
        Assert.True(viewModel.LockWhenMinimized);
        Assert.Equal(0, viewModel.ClipboardClearSeconds);
        Assert.Equal(45, viewModel.HidePasswordsAfterSeconds);
    }

    [Fact]
    public void ChangingOptions_PersistsImmediately()
    {
        var viewModel = CreateViewModel();
        viewModel.Open();

        viewModel.AutoLockMinutes = 30;
        viewModel.LockWhenMinimized = true;
        viewModel.ClipboardClearSeconds = 10;
        viewModel.HidePasswordsAfterSeconds = 5;

        var stored = new AppSettingsStore(_directory).Load();

        Assert.Equal(30, stored.AutoLockMinutes);
        Assert.True(stored.LockWhenMinimized);
        Assert.Equal(10, stored.ClipboardClearSeconds);
        Assert.Equal(5, stored.HidePasswordsAfterSeconds);
    }

    [Fact]
    public void Open_DoesNotRewriteTheStoredSettings()
    {
        _settings.Update(new AppSettings { AutoLockMinutes = 7 });
        var viewModel = CreateViewModel();

        viewModel.Open();

        Assert.Equal(7, _settings.Current.AutoLockMinutes);
        Assert.Equal(7, new AppSettingsStore(_directory).Load().AutoLockMinutes);
    }

    [Theory]
    [InlineData("", "new-password", "new-password", "current master password")]
    [InlineData("current", "short", "short", "at least")]
    [InlineData("current", "new-password", "other-password", "do not match")]
    public async Task ChangeMasterPassword_ValidatesTheInput(string current, string newPassword, string confirmation, string expectedMessage)
    {
        var viewModel = CreateViewModel();
        viewModel.Open();
        viewModel.CurrentPassword = current;
        viewModel.NewPassword = newPassword;
        viewModel.ConfirmPassword = confirmation;

        await viewModel.ChangeMasterPasswordCommand.ExecuteAsync(null);

        Assert.Contains(expectedMessage, viewModel.PasswordMessage);
        _rekey.Verify(service => service.ChangeMasterPasswordAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(RekeyResult.Success, "Master password changed.")]
    [InlineData(RekeyResult.InvalidCurrentPassword, "current master password is incorrect")]
    [InlineData(RekeyResult.Unavailable, "Unable to change")]
    public async Task ChangeMasterPassword_MapsTheResultAndClearsTheFields(RekeyResult result, string expectedMessage)
    {
        _rekey.Setup(service => service.ChangeMasterPasswordAsync("current", "new-password")).ReturnsAsync(result);
        var viewModel = CreateViewModel();
        viewModel.Open();
        viewModel.CurrentPassword = "current";
        viewModel.NewPassword = "new-password";
        viewModel.ConfirmPassword = "new-password";

        await viewModel.ChangeMasterPasswordCommand.ExecuteAsync(null);

        Assert.Contains(expectedMessage, viewModel.PasswordMessage);
        Assert.Empty(viewModel.CurrentPassword);
        Assert.Empty(viewModel.NewPassword);
        Assert.Empty(viewModel.ConfirmPassword);
    }

    [Fact]
    public async Task Backup_UsesThePickedPathAndReportsIt()
    {
        _picker.Setup(p => p.PickSaveFileAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("/tmp/backup.db");
        _backup.Setup(b => b.BackupAsync("/tmp/backup.db")).Returns(Task.CompletedTask);
        var viewModel = CreateViewModel();
        viewModel.Open();

        await viewModel.BackupVaultCommand.ExecuteAsync(null);

        _backup.Verify(b => b.BackupAsync("/tmp/backup.db"), Times.Once);
        Assert.Contains("/tmp/backup.db", viewModel.BackupMessage);
    }

    [Fact]
    public async Task Backup_WhenThePickerIsCancelled_DoesNothing()
    {
        _picker.Setup(p => p.PickSaveFileAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((string?)null);
        var viewModel = CreateViewModel();
        viewModel.Open();

        await viewModel.BackupVaultCommand.ExecuteAsync(null);

        _backup.Verify(b => b.BackupAsync(It.IsAny<string>()), Times.Never);
        Assert.False(viewModel.HasBackupMessage);
    }

    [Fact]
    public async Task Restore_RequiresConfirmationAndRaisesVaultRestored()
    {
        _picker.Setup(p => p.PickOpenFileAsync(It.IsAny<string>())).ReturnsAsync("/tmp/backup.db");
        _backup.Setup(b => b.RestoreAsync("/tmp/backup.db")).ReturnsAsync("/tmp/safety.bak");
        var viewModel = CreateViewModel();
        var restored = false;
        viewModel.VaultRestored += () => restored = true;
        viewModel.Open();

        await viewModel.ChooseRestoreCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasPendingRestore);
        _backup.Verify(b => b.RestoreAsync(It.IsAny<string>()), Times.Never);

        await viewModel.ConfirmRestoreCommand.ExecuteAsync(null);

        Assert.True(restored);
        Assert.False(viewModel.HasPendingRestore);
    }

    [Fact]
    public async Task Restore_WhenItFails_KeepsTheCurrentVaultAndReports()
    {
        _picker.Setup(p => p.PickOpenFileAsync(It.IsAny<string>())).ReturnsAsync("/tmp/broken.db");
        _backup.Setup(b => b.RestoreAsync("/tmp/broken.db")).ThrowsAsync(new InvalidDataException("not a backup"));
        var viewModel = CreateViewModel();
        var restored = false;
        viewModel.VaultRestored += () => restored = true;
        viewModel.Open();

        await viewModel.ChooseRestoreCommand.ExecuteAsync(null);
        await viewModel.ConfirmRestoreCommand.ExecuteAsync(null);

        Assert.False(restored);
        Assert.True(viewModel.HasBackupMessage);
        Assert.Contains("current vault was kept", viewModel.BackupMessage);
    }

    [Fact]
    public void CancelRestore_ClearsThePendingPath()
    {
        var viewModel = CreateViewModel();
        viewModel.Open();
        viewModel.PendingRestorePath = "/tmp/backup.db";

        viewModel.CancelRestoreCommand.Execute(null);

        Assert.False(viewModel.HasPendingRestore);
    }

    [Fact]
    public void Cancel_RaisesCloseRequested()
    {
        var viewModel = CreateViewModel();
        var closed = false;
        viewModel.CloseRequested += () => closed = true;

        viewModel.CancelCommand.Execute(null);

        Assert.True(closed);
    }
}
