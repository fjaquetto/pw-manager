using PWManager.Application.UseCases.Maintenance;
using PWManager.Infra.Security;

namespace PWManager.UnitTests;

public class VaultMaintenanceServiceTests
{
    [Fact]
    public async Task Backup_DelegatesToTheBackupAdapter()
    {
        using var database = await TestDatabase.CreateAsync();
        var maintenance = new VaultMaintenanceService(database.CreateBackupService(), new VaultSession());
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
        var maintenance = new VaultMaintenanceService(database.CreateBackupService(), session);
        var backup = Path.Combine(database.DirectoryPath, "backup.db");
        await maintenance.BackupAsync(backup);

        var safetyCopy = await maintenance.RestoreAsync(backup);

        Assert.False(session.IsUnlocked);
        Assert.True(File.Exists(safetyCopy));
    }

    [Fact]
    public async Task Restore_WhenValidationFails_DoesNotLockTheSession()
    {
        using var database = await TestDatabase.CreateAsync();
        var session = new VaultSession();
        session.Unlock(new byte[32]);
        var maintenance = new VaultMaintenanceService(database.CreateBackupService(), session);
        var invalid = Path.Combine(database.DirectoryPath, "invalid.db");
        await File.WriteAllTextAsync(invalid, "not a database");

        await Assert.ThrowsAsync<InvalidDataException>(() => maintenance.RestoreAsync(invalid));

        Assert.True(session.IsUnlocked);
    }
}
