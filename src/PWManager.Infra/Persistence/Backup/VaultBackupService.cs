using Microsoft.Data.Sqlite;
using PWManager.Domain.Abstractions.Infrastructure;

namespace PWManager.Infra.Persistence.Backup;

// VACUUM INTO produces a consistent snapshot without the WAL side files, and
// no plaintext ever leaves the vault.
public sealed class VaultBackupService(string databasePath) : IVaultBackupService
{
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
            await using var connection = new SqliteConnection(BuildConnectionString(sourcePath, readOnly: true));
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'User';";

            return Convert.ToInt64(await command.ExecuteScalarAsync()) > 0;
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

    public async Task<string> RestoreAsync(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var fullSource = Path.GetFullPath(sourcePath);

        if (string.Equals(fullSource, Path.GetFullPath(databasePath), PathComparison))
        {
            throw new InvalidOperationException("The selected file is the current vault.");
        }

        if (!await IsValidBackupAsync(fullSource))
        {
            throw new InvalidDataException("The selected file is not a PWManager backup.");
        }

        var safetyCopy = $"{databasePath}.before-restore-{DateTime.Now:yyyyMMddHHmmss}.bak";
        await BackupAsync(safetyCopy);

        SqliteConnection.ClearAllPools();
        var temporary = $"{databasePath}.restore-{Guid.NewGuid():N}.tmp";

        try
        {
            File.Copy(fullSource, temporary, overwrite: true);
            File.Move(temporary, databasePath, overwrite: true);
        }
        finally
        {
            TryDelete(temporary);
        }

        foreach (var sideFile in new[] { databasePath + "-wal", databasePath + "-shm" })
        {
            TryDelete(sideFile);
        }

        return safetyCopy;
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
