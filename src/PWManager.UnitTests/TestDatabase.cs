using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PWManager.Infra.Persistence;
using PWManager.Infra.Persistence.Schema;
using PWManager.Infra.Persistence.Repositories;
using PWManager.Infra.Persistence.Backup;

namespace PWManager.UnitTests;

internal sealed class TestDatabase : IDisposable
{
    public string DirectoryPath { get; }

    public string DatabasePath { get; }

    public IDbContextFactory<PWDbContext> ContextFactory { get; }

    public TestDatabase()
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), "pwmanager-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
        DatabasePath = Path.Combine(DirectoryPath, "vault.db");

        var options = new DbContextOptionsBuilder<PWDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString())
            .Options;

        ContextFactory = new TestDbContextFactory(options);
    }

    public static async Task<TestDatabase> CreateAsync()
    {
        var database = new TestDatabase();
        await database.MigrateAsync();

        return database;
    }

    public Task MigrateAsync() => new SqliteVaultSchemaMigrator(ContextFactory).MigrateAsync();

    public PWDbContext CreateContext() => ContextFactory.CreateDbContext();

    public UserRepository CreateUserRepository() => new(ContextFactory);

    public VaultMetadataRepository CreateMetadataRepository() => new(ContextFactory);

    public VaultMigrationRepository CreateMigrationRepository() => new(ContextFactory);

    public VaultBackupService CreateBackupService() => new(DatabasePath);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }

    private sealed class TestDbContextFactory(DbContextOptions<PWDbContext> options) : IDbContextFactory<PWDbContext>
    {
        public PWDbContext CreateDbContext() => new(options);

        public Task<PWDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
