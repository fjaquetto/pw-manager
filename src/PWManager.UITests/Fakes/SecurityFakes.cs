using PWManager.Application.UseCases.Import;
using PWManager.Application.UseCases.Maintenance;
using PWManager.Application.UseCases.Rekey;
using PWManager.Application.UseCases.Settings;
using PWManager.Domain.Settings;
using PWManager.Services;
using PWManager.Services.Interfaces;

namespace PWManager.UITests.Fakes;

internal sealed class AppSettingsFake : IAppSettingsService
{
    public AppSettings Current { get; private set; } = new();

    public void Update(AppSettings settings) => Current = settings.Normalized();
}

internal sealed class RekeyStub : IVaultRekeyService
{
    public RekeyResult Result { get; set; } = RekeyResult.Success;

    public Task<RekeyResult> ChangeMasterPasswordAsync(string currentPassword, string newPassword) => Task.FromResult(Result);
}

internal sealed class BackupStub : IVaultMaintenanceService
{
    public string? LastBackupPath { get; private set; }

    public Task BackupAsync(string targetPath)
    {
        LastBackupPath = targetPath;

        return Task.CompletedTask;
    }

    public Task<string> RestoreAsync(string sourcePath) => Task.FromResult(sourcePath + ".safety");
}

internal sealed class ImportStub : IVaultImportService
{
    public VaultImportResult Result { get; set; } = new(VaultImportStatus.Success, Added: 1);

    public Task<VaultImportResult> ImportAsync(string sourcePath, string sourcePassword) => Task.FromResult(Result);
}

internal sealed class FilePickerStub : IFilePickerService
{
    public string? SavePath { get; set; }

    public string? OpenPath { get; set; }

    public Task<FilePickerResult> PickSaveFileAsync(string title, string suggestedFileName) =>
        Task.FromResult(SavePath is null ? FilePickerResult.Canceled() : FilePickerResult.Selected(SavePath));

    public Task<FilePickerResult> PickOpenFileAsync(string title) =>
        Task.FromResult(OpenPath is null ? FilePickerResult.Canceled() : FilePickerResult.Selected(OpenPath));
}
