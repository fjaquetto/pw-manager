using PWManager.Application.UseCases.Import;
using PWManager.Application.UseCases.VaultEntries;
using PWManager.Domain.Abstractions.Infrastructure;
using PWManager.Domain.Vault;
using PWManager.Infra.Persistence.Archives;
using PWManager.Infra.Persistence.Repositories;
using PWManager.Infra.Persistence.Schema;
using PWManager.Infra.Security;

namespace PWManager.UnitTests;

public class VaultImportServiceTests
{
    [Fact]
    public async Task Import_AddsEntriesFromAnotherVaultAndKeepsTheDestination()
    {
        using var destination = await TestDatabase.CreateAsync();
        using var source = await TestDatabase.CreateAsync();
        var destinationSession = await CreateVault(destination, "destination-password", ("Destination", "dest@example.com", "dest-secret"));
        await CreateVault(source, "source-password", ("Source", "source@example.com", "source-secret"));
        var sourceBackup = Path.Combine(source.DirectoryPath, "source-backup.db");
        await source.CreateBackupService().BackupAsync(sourceBackup);

        var destinationEntries = CreateEntryService(destination, destinationSession);
        var import = CreateImportService(destination, destinationSession, destinationEntries);

        var result = await import.ImportAsync(sourceBackup, "source-password");

        Assert.Equal(VaultImportStatus.Success, result.Status);
        Assert.Equal(1, result.Added);
        Assert.NotNull(result.SafetyCopyPath);
        Assert.True(File.Exists(result.SafetyCopyPath));

        var entries = await destinationEntries.LoadAsync();
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, entry => entry.Site == "Source" && entry.Password == "source-secret");
        Assert.Contains(entries, entry => entry.Site == "Destination" && entry.Password == "dest-secret");
    }

    [Fact]
    public async Task Import_UpdatesAnEntryWhenTheSourceVersionIsNewer()
    {
        using var destination = await TestDatabase.CreateAsync();
        using var source = await TestDatabase.CreateAsync();
        var destinationSession = await CreateVault(destination, "destination-password", ("Original", "original@example.com", "original-secret"));
        var destinationEntries = CreateEntryService(destination, destinationSession);
        var destinationEntry = (await destinationEntries.LoadAsync())[0];
        var sourceSession = await CreateVault(source, "source-password");
        await source.CreateUserRepository().AddAsync(new UserEncryptorService(sourceSession, new VaultCipherService()).EncryptUser(new User
        {
            Id = destinationEntry.Id,
            Site = "Updated",
            Login = "updated@example.com",
            Password = "updated-secret",
            CreationDate = destinationEntry.CreationDate,
            LastUpdated = destinationEntry.LastUpdated.AddHours(1)
        }));
        var sourceBackup = Path.Combine(source.DirectoryPath, "source-backup.db");
        await source.CreateBackupService().BackupAsync(sourceBackup);
        var import = CreateImportService(destination, destinationSession, destinationEntries);

        var result = await import.ImportAsync(sourceBackup, "source-password");

        Assert.Equal(VaultImportStatus.Success, result.Status);
        Assert.Equal(1, result.Updated);
        var updated = (await destinationEntries.LoadAsync()).Single(entry => entry.Id == destinationEntry.Id);
        Assert.Equal("Updated", updated.Site);
        Assert.Equal("updated-secret", updated.Password);
    }

    [Fact]
    public async Task Import_KeepsTheDestinationOnAConflict()
    {
        using var destination = await TestDatabase.CreateAsync();
        using var source = await TestDatabase.CreateAsync();
        var destinationSession = await CreateVault(destination, "destination-password", ("Original", "original@example.com", "original-secret"));
        var destinationEntries = CreateEntryService(destination, destinationSession);
        var destinationEntry = (await destinationEntries.LoadAsync())[0];
        var sourceSession = await CreateVault(source, "source-password");
        await source.CreateUserRepository().AddAsync(new UserEncryptorService(sourceSession, new VaultCipherService()).EncryptUser(new User
        {
            Id = destinationEntry.Id,
            Site = "Conflicting",
            Login = "conflict@example.com",
            Password = "conflict-secret",
            CreationDate = destinationEntry.CreationDate,
            LastUpdated = destinationEntry.LastUpdated
        }));
        var sourceBackup = Path.Combine(source.DirectoryPath, "source-backup.db");
        await source.CreateBackupService().BackupAsync(sourceBackup);
        var import = CreateImportService(destination, destinationSession, destinationEntries);

        var result = await import.ImportAsync(sourceBackup, "source-password");

        Assert.Equal(VaultImportStatus.Success, result.Status);
        Assert.Equal(1, result.Conflicts);
        var unchanged = (await destinationEntries.LoadAsync()).Single(entry => entry.Id == destinationEntry.Id);
        Assert.Equal("Original", unchanged.Site);
        Assert.Equal("original-secret", unchanged.Password);
    }

    [Fact]
    public async Task Import_WithTheWrongPassword_DoesNotChangeTheDestination()
    {
        using var destination = await TestDatabase.CreateAsync();
        using var source = await TestDatabase.CreateAsync();
        var destinationSession = await CreateVault(destination, "destination-password", ("Destination", "dest@example.com", "dest-secret"));
        await CreateVault(source, "source-password", ("Source", "source@example.com", "source-secret"));
        var sourceBackup = Path.Combine(source.DirectoryPath, "source-backup.db");
        await source.CreateBackupService().BackupAsync(sourceBackup);

        var destinationEntries = CreateEntryService(destination, destinationSession);
        var import = CreateImportService(destination, destinationSession, destinationEntries);

        var result = await import.ImportAsync(sourceBackup, "wrong-password");

        Assert.Equal(VaultImportStatus.InvalidPassword, result.Status);
        var entries = await destinationEntries.LoadAsync();
        Assert.Single(entries);
        Assert.Equal("Destination", entries[0].Site);
    }

    private static VaultImportService CreateImportService(
        TestDatabase destination,
        VaultSession destinationSession,
        IVaultEntryService destinationEntries) =>
        new(
            destination.CreateBackupService(),
            new SqliteVaultSchemaMigrator(destination.ContextFactory),
            new VaultArchiveReader(),
            new VaultKeyService(),
            new VaultCipherService(),
            new LegacyVaultCipherService(),
            destinationEntries,
            new UserEncryptorService(destinationSession, new VaultCipherService()),
            new VaultMergeRepository(destination.ContextFactory),
            TimeProvider.System);

    private static IVaultEntryService CreateEntryService(TestDatabase database, VaultSession session) =>
        new VaultEntryService(
            database.CreateUserRepository(),
            new UserEncryptorService(session, new VaultCipherService()),
            TimeProvider.System);

    private static async Task<VaultSession> CreateVault(
        TestDatabase database,
        string password,
        params (string Site, string Login, string Password)[] entries)
    {
        var keyService = new VaultKeyService();
        var salt = keyService.CreateSalt();
        var key = keyService.DeriveKey(password, salt, 100_000);
        var session = new VaultSession();
        session.Unlock(key);

        await database.CreateMetadataRepository().AddAsync(new VaultMetadata
        {
            Id = 1,
            KdfSalt = Convert.ToBase64String(salt),
            KdfIterations = 100_000,
            Verifier = keyService.CreateVerifier(key),
            CreatedAt = DateTime.Now
        });

        var entryService = CreateEntryService(database, session);

        foreach (var entry in entries)
        {
            await entryService.SaveAsync(null, entry.Site, entry.Login, entry.Password);
        }

        return session;
    }
}
