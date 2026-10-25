using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PWManager.Domain.Vault;
using PWManager.Infra.Persistence;
using PWManager.Infra.Security;

namespace PWManager.UnitTests;

public class VaultBackupServiceTests
{
    [Fact]
    public async Task Backup_ProducesASingleConsistentFile()
    {
        using var database = await TestDatabase.CreateAsync();
        AddEntry(database, "GitHub");

        var target = Path.Combine(database.DirectoryPath, "backup.db");
        var service = database.CreateBackupService();

        await service.BackupAsync(target);

        Assert.True(File.Exists(target));
        Assert.False(File.Exists(target + "-wal"));
        Assert.False(File.Exists(target + "-shm"));
        Assert.True(await service.IsValidBackupAsync(target));

        await using var connection = new SqliteConnection($"Data Source={target};Mode=ReadOnly");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM User;";

        Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task Backup_ReplacesAnExistingDestinationAfterSuccess()
    {
        using var database = await TestDatabase.CreateAsync();
        AddEntry(database, "GitHub");

        var target = Path.Combine(database.DirectoryPath, "backup.db");
        await File.WriteAllTextAsync(target, "old backup");
        var service = database.CreateBackupService();

        await service.BackupAsync(target);

        Assert.True(await service.IsValidBackupAsync(target));
        Assert.False(Directory.EnumerateFiles(database.DirectoryPath, "*.tmp-*").Any());
    }

    [Fact]
    public async Task Restore_ReplacesTheVaultAndKeepsASafetyCopy()
    {
        using var database = await TestDatabase.CreateAsync();
        AddEntry(database, "GitHub");

        var backup = Path.Combine(database.DirectoryPath, "backup.db");
        var service = database.CreateBackupService();
        await service.BackupAsync(backup);

        AddEntry(database, "GitLab");
        Assert.Equal(2, CountEntries(database));

        var safetyCopy = await service.RestoreAsync(backup);

        Assert.Equal(1, CountEntries(database));
        Assert.True(File.Exists(safetyCopy));
        Assert.Equal(2, CountEntries(safetyCopy));
        Assert.False(File.Exists(database.DatabasePath + "-wal"));
    }

    [Fact]
    public async Task Backup_RefusesToOverwriteTheLiveDatabase()
    {
        using var database = await TestDatabase.CreateAsync();
        AddEntry(database, "GitHub");
        var service = database.CreateBackupService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.BackupAsync(database.DatabasePath));
    }

    [Fact]
    public async Task Restore_RejectsFilesThatAreNotVaults()
    {
        using var database = await TestDatabase.CreateAsync();
        AddEntry(database, "GitHub");
        var foreign = Path.Combine(database.DirectoryPath, "foreign.db");
        await File.WriteAllTextAsync(foreign, "this is not a database");
        var service = database.CreateBackupService();

        Assert.False(await service.IsValidBackupAsync(foreign));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.RestoreAsync(foreign));
        Assert.Equal(1, CountEntries(database));
    }

    private static void AddEntry(TestDatabase database, string site)
    {
        var session = new VaultSession();
        session.Unlock(VaultCrypto.DeriveKey("password", VaultCrypto.CreateSalt(), 1000));

        using var context = database.CreateContext();
        context.Users.Add(new UserEncryptorService(session, new VaultCipherService())
            .EncryptUser(new User(site, "login", "secret")));
        context.SaveChanges();
    }

    private static int CountEntries(TestDatabase database)
    {
        using var context = database.CreateContext();

        return context.Users.Count();
    }

    private static int CountEntries(string path)
    {
        var options = new DbContextOptionsBuilder<PWDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;

        using var context = new PWDbContext(options);

        return context.Users.Count();
    }
}
