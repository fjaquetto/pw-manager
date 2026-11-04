using PWManager.Domain.Abstractions.Infrastructure;

namespace PWManager.Application.UseCases.Maintenance;

public sealed class VaultMaintenanceService(
    IVaultBackupService backupService,
    IVaultSchemaMigrator schemaMigrator,
    IVaultSession session) : IVaultMaintenanceService
{
    public Task BackupAsync(string targetPath) => backupService.BackupAsync(targetPath);

    public Task<string> RestoreAsync(string sourcePath) => Task.Run(() => RestoreCoreAsync(sourcePath));

    private async Task<string> RestoreCoreAsync(string sourcePath)
    {
        var stagedPath = await backupService.StageRestoreAsync(sourcePath);

        try
        {
            await schemaMigrator.MigrateAsync(stagedPath);

            var safetyCopy = await backupService.CommitRestoreAsync(stagedPath);

            session.Lock();

            return safetyCopy;
        }
        catch
        {
            await backupService.DiscardStagedAsync(stagedPath);
            throw;
        }
    }
}
