
using PWManager.Domain.Abstractions.Infrastructure;

namespace PWManager.Application.UseCases.Maintenance;

public sealed class VaultMaintenanceService(
    IVaultBackupService backupService,
    IVaultSession session) : IVaultMaintenanceService
{
    public Task BackupAsync(string targetPath) => backupService.BackupAsync(targetPath);

    public async Task<string> RestoreAsync(string sourcePath)
    {
        var safetyCopy = await backupService.RestoreAsync(sourcePath);

        // The restored vault belongs to a different master password, so the
        // current key material must not survive the operation.
        session.Lock();

        return safetyCopy;
    }
}
