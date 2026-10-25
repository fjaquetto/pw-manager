namespace PWManager.Domain.Abstractions.Infrastructure;

public interface IVaultBackupService
{
    string DatabasePath { get; }

    Task BackupAsync(string targetPath);

    Task<bool> IsValidBackupAsync(string sourcePath);

    Task<string> RestoreAsync(string sourcePath);
}
