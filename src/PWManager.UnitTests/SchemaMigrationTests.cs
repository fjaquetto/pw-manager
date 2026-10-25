using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PWManager.Domain.Vault;

namespace PWManager.UnitTests;

public class SchemaMigrationTests
{
    private const string LastLegacyMigration = "20250309002358_RemoveIsActiveColumn";

    [Fact]
    public async Task FreshDatabase_AppliesAllMigrations()
    {
        using var database = await TestDatabase.CreateAsync();

        await using var context = database.CreateContext();
        var applied = await context.Database.GetAppliedMigrationsAsync();

        Assert.Contains("AddVaultMetadata", applied.Select(GetMigrationName));
        Assert.Contains("InitialCreate", applied.Select(GetMigrationName));
        Assert.Contains("AddLastUpdatedColumn", applied.Select(GetMigrationName));
        Assert.Contains("RemoveIsActiveColumn", applied.Select(GetMigrationName));
    }

    [Fact]
    public async Task EnsureCreatedDatabaseWithoutHistory_IsBaselinedAndPreservesData()
    {
        using var database = new TestDatabase();
        var user = new User("Site", "login", "password");

        await using (var context = database.CreateContext())
        {
            await context.Database.EnsureCreatedAsync();
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        await database.MigrateAsync();

        await using (var context = database.CreateContext())
        {
            var stored = Assert.Single(context.Users.ToList());

            Assert.Equal(user.Id, stored.Id);
            Assert.Empty(context.VaultMetadata.ToList());

            var applied = (await context.Database.GetAppliedMigrationsAsync()).Select(GetMigrationName).ToArray();

            Assert.Contains("InitialCreate", applied);
            Assert.Contains("AddLastUpdatedColumn", applied);
            Assert.Contains("RemoveIsActiveColumn", applied);
            Assert.Contains("AddVaultMetadata", applied);
        }

        // Running the initializer again must be a no-op.
        await database.MigrateAsync();
    }

    [Fact]
    public async Task EnsureCreatedDatabaseWithoutMetadataTable_AddsVaultMetadata()
    {
        using var database = new TestDatabase();

        await using (var context = database.CreateContext())
        {
            await context.Database.EnsureCreatedAsync();
            await context.Database.ExecuteSqlRawAsync("DROP TABLE VaultMetadata;");
            context.Users.Add(new User("Site", "login", "password"));
            await context.SaveChangesAsync();
        }

        await database.MigrateAsync();

        await using (var context = database.CreateContext())
        {
            Assert.Single(context.Users.ToList());
            await context.Database.ExecuteSqlRawAsync("SELECT COUNT(*) FROM VaultMetadata;");

            var applied = (await context.Database.GetAppliedMigrationsAsync()).Select(GetMigrationName).ToArray();

            Assert.Contains("AddVaultMetadata", applied);
        }
    }

    [Fact]
    public async Task DatabaseWithLegacyMigrationHistory_OnlyAppliesTheNewMigration()
    {
        using var database = new TestDatabase();

        await using (var context = database.CreateContext())
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(LastLegacyMigration);

            context.Users.Add(new User("Site", "login", "password"));
            await context.SaveChangesAsync();
        }

        await database.MigrateAsync();

        await using (var context = database.CreateContext())
        {
            Assert.Single(context.Users.ToList());
            await context.Database.ExecuteSqlRawAsync("SELECT COUNT(*) FROM VaultMetadata;");

            var applied = (await context.Database.GetAppliedMigrationsAsync()).Select(GetMigrationName).ToArray();

            Assert.Contains("AddVaultMetadata", applied);
        }
    }

    private static string GetMigrationName(string migrationId)
    {
        var separator = migrationId.IndexOf('_');

        return separator >= 0 ? migrationId[(separator + 1)..] : migrationId;
    }
}
