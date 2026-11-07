using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PWManager.Application.UseCases.Maintenance;
using PWManager.Domain.Vault;
using PWManager.Infra.Persistence;
using PWManager.Infra.Persistence.Backup;
using PWManager.Infra.Persistence.Schema;
using PWManager.Infra.Security;

namespace PWManager.UnitTests;

public class VaultMaintenanceServiceTests
{
    [Fact]
    public async Task Backup_DelegatesToTheBackupAdapter()
    {
        using var database = await TestDatabase.CreateAsync();
        var maintenance = new VaultMaintenanceService(
            database.CreateBackupService(),
            new SqliteVaultSchemaMigrator(database.ContextFactory),
            new VaultSession());
        var target = Path.Combine(database.DirectoryPath, "backup.db");

        await maintenance.BackupAsync(target);

        Assert.True(File.Exists(target));
        Assert.True(await database.CreateBackupService().IsValidBackupAsync(target));
    }

    [Fact]
    public async Task Restore_LocksTheSessionAndReturnsTheSafetyCopy()
    {
        using var database = await TestDatabase.CreateAsync();
        var session = new VaultSession();
        session.Unlock(new byte[32]);
        var maintenance = new VaultMaintenanceService(
            database.CreateBackupService(),
            new SqliteVaultSchemaMigrator(database.ContextFactory),
            session);
        var backup = Path.Combine(database.DirectoryPath, "backup.db");
        await maintenance.BackupAsync(backup);

        var safetyCopy = await maintenance.RestoreAsync(backup);

        Assert.False(session.IsUnlocked);
        Assert.True(File.Exists(safetyCopy));
    }

    [Fact]
    public async Task Restore_MigratesAnOlderBackupBeforeReplacingTheVault()
    {
        using var database = await TestDatabase.CreateAsync();
        var legacyPath = Path.Combine(database.DirectoryPath, "legacy.db");
        var options = new DbContextOptionsBuilder<PWDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = legacyPath }.ToString())
            .Options;

        await using (var legacyContext = new PWDbContext(options))
        {
            await legacyContext.Database.MigrateAsync("20250309002358_RemoveIsActiveColumn");
            legacyContext.Users.Add(new User("site", "login", "password"));
            await legacyContext.SaveChangesAsync();
        }

        var backup = Path.Combine(database.DirectoryPath, "legacy-backup.db");
        await new VaultBackupService(legacyPath, TimeProvider.System).BackupAsync(backup);
        var session = new VaultSession();
        session.Unlock(new byte[32]);
        var maintenance = new VaultMaintenanceService(
            database.CreateBackupService(),
            new SqliteVaultSchemaMigrator(database.ContextFactory),
            session);

        await maintenance.RestoreAsync(backup);

        using var context = database.CreateContext();
        var migrations = await context.Database.GetAppliedMigrationsAsync();
        Assert.Contains(migrations, migration => migration.EndsWith("AddVaultMetadata", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Restore_WhenValidationFails_DoesNotLockTheSession()
    {
        using var database = await TestDatabase.CreateAsync();
        var session = new VaultSession();
        session.Unlock(new byte[32]);
        var maintenance = new VaultMaintenanceService(
            database.CreateBackupService(),
            new SqliteVaultSchemaMigrator(database.ContextFactory),
            session);
        var invalid = Path.Combine(database.DirectoryPath, "invalid.db");
        await File.WriteAllTextAsync(invalid, "not a database");

        await Assert.ThrowsAsync<InvalidDataException>(() => maintenance.RestoreAsync(invalid));

        Assert.True(session.IsUnlocked);
    }
}
