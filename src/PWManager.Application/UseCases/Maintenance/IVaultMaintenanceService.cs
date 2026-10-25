namespace PWManager.Application.UseCases.Maintenance;

public interface IVaultMaintenanceService
{
    Task BackupAsync(string targetPath);

    Task<string> RestoreAsync(string sourcePath);
}
