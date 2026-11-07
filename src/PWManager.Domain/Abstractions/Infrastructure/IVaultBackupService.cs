namespace PWManager.Domain.Abstractions.Infrastructure;

public interface IVaultBackupService
{
    string DatabasePath { get; }

    Task BackupAsync(string targetPath);

    Task<bool> IsValidBackupAsync(string sourcePath);

    Task<string> StageRestoreAsync(string sourcePath);

    Task<string> CommitRestoreAsync(string stagedPath);

    Task DiscardStagedAsync(string stagedPath);

    Task<string> RestoreAsync(string sourcePath);
}
