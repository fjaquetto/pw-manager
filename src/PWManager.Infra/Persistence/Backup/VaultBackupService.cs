using Microsoft.Data.Sqlite;
using PWManager.Domain.Abstractions.Infrastructure;

namespace PWManager.Infra.Persistence.Backup;

// VACUUM INTO produces a consistent snapshot without the WAL side files, and
// no plaintext ever leaves the vault.
public sealed class VaultBackupService(string databasePath) : IVaultBackupService
{
    private const long MaxBackupBytes = 1024L * 1024 * 1024;

    public string DatabasePath => databasePath;

    public async Task BackupAsync(string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        var fullTarget = Path.GetFullPath(targetPath);

        if (string.Equals(fullTarget, Path.GetFullPath(databasePath), PathComparison))
        {
            throw new InvalidOperationException("Choose a different file for the backup.");
        }

        var directory = Path.GetDirectoryName(fullTarget);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = $"{fullTarget}.tmp-{Guid.NewGuid():N}";

        try
        {
            await using var connection = new SqliteConnection(BuildConnectionString(databasePath));
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "VACUUM INTO $target;";
            command.Parameters.AddWithValue("$target", temporary);
            await command.ExecuteNonQueryAsync();

            File.Move(temporary, fullTarget, overwrite: true);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    public async Task<bool> IsValidBackupAsync(string sourcePath)
    {
        if (!File.Exists(sourcePath))
        {
            return false;
        }

        try
        {
            var file = new FileInfo(sourcePath);

            if (file.Length == 0 || file.Length > MaxBackupBytes)
            {
                return false;
            }

            await using var connection = new SqliteConnection(BuildConnectionString(sourcePath, readOnly: true));
            await connection.OpenAsync();

            await using var integrity = connection.CreateCommand();
            integrity.CommandText = "PRAGMA quick_check;";

            if (!string.Equals(Convert.ToString(await integrity.ExecuteScalarAsync()), "ok", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'User';";

            if (Convert.ToInt64(await command.ExecuteScalarAsync()) == 0)
            {
                return false;
            }

            await using var columns = connection.CreateCommand();
            columns.CommandText =
                "SELECT COUNT(*) FROM pragma_table_info('User') WHERE name IN ('Site', 'Login', 'Password', 'CreationDate');";

            return Convert.ToInt64(await columns.ExecuteScalarAsync()) == 4;
        }
        catch (SqliteException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public async Task<string> StageRestoreAsync(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var fullSource = Path.GetFullPath(sourcePath);

        if (string.Equals(fullSource, Path.GetFullPath(databasePath), PathComparison))
        {
            throw new InvalidOperationException("The selected file is the current vault.");
        }

        if (!await IsValidBackupAsync(fullSource))
        {
            throw new InvalidDataException("The selected file is not a valid PWManager vault.");
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var staged = $"{databasePath}.restore-{Guid.NewGuid():N}.tmp";

        try
        {
            File.Copy(fullSource, staged, overwrite: true);
            return staged;
        }
        catch
        {
            TryDelete(staged);
            throw;
        }
    }

    public async Task<string> CommitRestoreAsync(string stagedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagedPath);

        var fullStaged = Path.GetFullPath(stagedPath);

        if (!File.Exists(fullStaged))
        {
            throw new InvalidDataException("The prepared backup is no longer available.");
        }

        var safetyCopy = $"{databasePath}.before-restore-{DateTime.Now:yyyyMMddHHmmss}.bak";
        await BackupAsync(safetyCopy);

        SqliteConnection.ClearAllPools();
        File.Move(fullStaged, databasePath, overwrite: true);

        foreach (var sideFile in new[] { databasePath + "-wal", databasePath + "-shm" })
        {
            TryDelete(sideFile);
        }

        return safetyCopy;
    }

    public Task DiscardStagedAsync(string stagedPath)
    {
        TryDelete(stagedPath);
        return Task.CompletedTask;
    }

    public async Task<string> RestoreAsync(string sourcePath)
    {
        var staged = await StageRestoreAsync(sourcePath);

        try
        {
            return await CommitRestoreAsync(staged);
        }
        catch
        {
            TryDelete(staged);
            throw;
        }
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static string BuildConnectionString(string path, bool readOnly = false) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString();

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
