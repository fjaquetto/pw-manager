using Microsoft.Data.Sqlite;
using Moq;
using PWManager.Application.UseCases.Bootstrapping;
using PWManager.Application.UseCases.Migration;
using PWManager.Application.UseCases.Unlock;
using PWManager.Domain.Vault;
using PWManager.Infra.Persistence.Schema;
using PWManager.Infra.Security;

namespace PWManager.UnitTests;

public class VaultBootstrapperTests
{
    [Fact]
    public async Task InitializeAsync_FreshVault_CreatesSchemaAndReportsNew()
    {
        using var database = new TestDatabase();
        var status = await CreateBootstrapper(database).InitializeAsync();

        Assert.Equal(VaultStatus.New, status);
        Assert.True(File.Exists(database.DatabasePath));

        var tables = ReadTableNames(database);

        Assert.Contains("User", tables);
        Assert.Contains("VaultMetadata", tables);
        Assert.Contains("__EFMigrationsHistory", tables);
    }

    [Fact]
    public async Task InitializeAsync_ExistingVault_ReportsExisting()
    {
        using var database = new TestDatabase();
        await CreateBootstrapper(database).InitializeAsync();
        CreateVault(database, "correct-example");

        var status = await CreateBootstrapper(database).InitializeAsync();

        Assert.Equal(VaultStatus.Existing, status);
    }

    [Fact]
    public async Task InitializeAsync_VaultWithEntriesButNoMetadata_ReportsExisting()
    {
        using var database = new TestDatabase();
        await database.MigrateAsync();

        await using (var context = database.CreateContext())
        {
            context.Users.Add(new User("site", "login", "password"));
            await context.SaveChangesAsync();
        }

        var status = await CreateBootstrapper(database).InitializeAsync();

        Assert.Equal(VaultStatus.Existing, status);
    }

    [Fact(Timeout = 10000)]
    public async Task InitializeAsync_DoesNotRunOnTheCallingThread()
    {
        using var database = await TestDatabase.CreateAsync();
        var callerThread = Environment.CurrentManagedThreadId;
        using var gate = new ManualResetEventSlim(false);
        var workerThread = 0;

        var unlockService = new Mock<IUnlockService>();
        unlockService
            .Setup(service => service.GetStatusAsync())
            .Returns(() =>
            {
                workerThread = Environment.CurrentManagedThreadId;
                gate.Wait();

                return Task.FromResult(VaultStatus.Existing);
            });

        var task = CreateBootstrapper(database, unlockService.Object).InitializeAsync();

        Assert.False(task.IsCompleted);
        gate.Set();

        Assert.Equal(VaultStatus.Existing, await task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.NotEqual(callerThread, workerThread);
    }

    private static VaultBootstrapper CreateBootstrapper(TestDatabase database, IUnlockService? unlockService = null) =>
        new(new SqliteVaultSchemaMigrator(database.ContextFactory), unlockService ?? CreateUnlockService(database));

    private static UnlockService CreateUnlockService(TestDatabase database)
    {
        return new UnlockService(
            database.CreateMetadataRepository(),
            database.CreateUserRepository(),
            new VaultKeyService(),
            new VaultMigrationService(
                database.CreateMigrationRepository(),
                database.CreateBackupService(),
                new VaultCipherService(),
                new LegacyVaultCipherService()),
            new VaultSession(),
            TimeProvider.System);
    }

    private static void CreateVault(TestDatabase database, string password)
    {
        var result = CreateUnlockService(database).UnlockAsync(password).GetAwaiter().GetResult();

        Assert.Equal(UnlockResult.Success, result);
    }

    private static List<string> ReadTableNames(TestDatabase database)
    {
        using var connection = new SqliteConnection($"Data Source={database.DatabasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";

        var names = new List<string>();

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
