using System.Globalization;
using Microsoft.Data.Sqlite;
using PWManager.Domain.Abstractions.Infrastructure;

namespace PWManager.Infra.Persistence.Backup;

// VACUUM INTO produces a consistent snapshot without the WAL side files, and
// no plaintext ever leaves the vault.
public sealed class VaultBackupService(string databasePath, TimeProvider timeProvider) : IVaultBackupService
{
    private const long MaxBackupBytes = 1024L * 1024 * 1024;
    private static readonly string[] RequiredUserColumns = ["Site", "Login", "Password", "CreationDate"];

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
            if (!HasSupportedSize(sourcePath))
            {
                return false;
            }

            await using var connection = new SqliteConnection(BuildConnectionString(sourcePath, readOnly: true));
            await connection.OpenAsync();

            return await PassesIntegrityCheckAsync(connection)
                && await HasRequiredUserSchemaAsync(connection);
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

        var safetyCopy = CreateSafetyCopyPath("before-restore");
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

    private static bool HasSupportedSize(string sourcePath)
    {
        var file = new FileInfo(sourcePath);

        return file.Length is > 0 and <= MaxBackupBytes;
    }

    private static async Task<bool> PassesIntegrityCheckAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";

        return string.Equals(
            Convert.ToString(await command.ExecuteScalarAsync()),
            "ok",
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> HasRequiredUserSchemaAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'User';";

        if (Convert.ToInt64(await command.ExecuteScalarAsync()) == 0)
        {
            return false;
        }

        var columns = await ReadColumnsAsync(connection);
        return RequiredUserColumns.All(column => columns.Contains(column));
    }

    private static async Task<HashSet<string>> ReadColumnsAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info('User');";

        await using var reader = await command.ExecuteReaderAsync();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private string CreateSafetyCopyPath(string prefix) =>
        $"{databasePath}.{prefix}-{timeProvider.GetLocalNow().ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture)}.bak";

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
