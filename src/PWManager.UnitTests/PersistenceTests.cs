using Microsoft.EntityFrameworkCore;
using Moq;
using PWManager.Application.UseCases.Migration;
using PWManager.Application.UseCases.Settings;
using PWManager.Application.UseCases.Unlock;
using PWManager.Application.UseCases.VaultEntries;
using PWManager.Application.UseCases.Maintenance;
using PWManager.Application.UseCases.Rekey;
using PWManager.Domain.Abstractions.Infrastructure;
using PWManager.Domain.Vault;
using PWManager.Infra.Configuration;
using PWManager.Infra.Security;
using PWManager.Services.Interfaces;
using PWManager.ViewModels;

namespace PWManager.UnitTests;

public class PersistenceTests
{
    [Fact]
    public async Task TemporarySqlite_CreateEditReopenDelete_PreservesEncryptionAndDates()
    {
        using var database = await TestDatabase.CreateAsync();
        var session = CreateUnlockedSession("Only-a-test-master-password");

        Guid id;
        DateTime created;

        using (var viewModel = CreateViewModel(database, session))
        {
            await viewModel.LoadEntriesAsync();
            viewModel.NewEntryCommand.Execute(null);
            viewModel.Editor.Site = "Test account";
            viewModel.Editor.Login = "alex@example.com";
            viewModel.Editor.Password = "  keep these spaces  ";

            await viewModel.SaveEntryCommand.ExecuteAsync(null);

            Assert.False(viewModel.Notification.IsError);
            id = viewModel.SelectedEntry!.Id;
            created = viewModel.SelectedEntry.CreationDate;
            var encrypted = await database.CreateUserRepository().GetUserByIdAsync(id);

            Assert.NotNull(encrypted);
            Assert.True(VaultCrypto.IsV2(encrypted.Site));
            Assert.NotEqual("Test account", encrypted.Site);
            Assert.NotEqual("alex@example.com", encrypted.Login);
            Assert.NotEqual("  keep these spaces  ", encrypted.Password);
            viewModel.EditEntryCommand.Execute(null);
            viewModel.Editor.Login = "updated@example.com";

            await viewModel.SaveEntryCommand.ExecuteAsync(null);

            Assert.Equal(created, viewModel.SelectedEntry!.CreationDate);
        }

        using (var viewModel = CreateViewModel(database, session))
        {
            await viewModel.LoadEntriesAsync();
            var restored = Assert.Single(viewModel.Entries);

            Assert.Equal(id, restored.Id);
            Assert.Equal(created, restored.CreationDate);
            Assert.Equal("updated@example.com", restored.Login);
            Assert.Equal("  keep these spaces  ", restored.Password);
            viewModel.SelectedEntry = restored;
            viewModel.RequestDeleteCommand.Execute(null);

            await viewModel.ConfirmDeleteCommand.ExecuteAsync(null);

            Assert.Empty(await database.CreateUserRepository().GetAllAsync());
        }
    }

    [Fact]
    public async Task UnlockService_EmptyVault_CreatesV2MetadataAndUnlocks()
    {
        using var database = await TestDatabase.CreateAsync();
        var session = new VaultSession();
        var service = CreateUnlockService(database, session);

        Assert.Equal(VaultStatus.New, await service.GetStatusAsync());
        Assert.Equal(UnlockResult.Success, await service.UnlockAsync("first-example-password"));

        Assert.True(session.IsUnlocked);
        Assert.Equal(VaultStatus.Existing, await service.GetStatusAsync());

        await using var context = database.CreateContext();
        var metadata = Assert.Single(context.VaultMetadata.ToList());

        Assert.Equal(VaultMetadata.CurrentFormatVersion, metadata.FormatVersion);
        Assert.Equal(VaultMetadata.Pbkdf2Sha256, metadata.Kdf);
        Assert.Equal(VaultMetadata.DefaultIterations, metadata.KdfIterations);
        Assert.True(VaultCrypto.VerifyKey(session.GetKey(), metadata.Verifier));
    }

    [Fact]
    public async Task UnlockService_ExistingV2Vault_RequiresTheCorrectPassword()
    {
        using var database = await TestDatabase.CreateAsync();
        var creator = new VaultSession();
        var createService = CreateUnlockService(database, creator);

        Assert.Equal(UnlockResult.Success, await createService.UnlockAsync("correct-example"));

        var session = new VaultSession();
        var service = CreateUnlockService(database, session);

        Assert.Equal(VaultStatus.Existing, await service.GetStatusAsync());
        Assert.Equal(UnlockResult.InvalidPassword, await service.UnlockAsync("wrong-example"));
        Assert.False(session.IsUnlocked);
        Assert.Equal(UnlockResult.Success, await service.UnlockAsync("correct-example"));
        Assert.True(session.IsUnlocked);
    }

    [Fact]
    public async Task UnlockService_MigratesLegacyVault_AndIsIdempotent()
    {
        using var database = await TestDatabase.CreateAsync();
        var legacyEntry = CreateLegacyEntry("correct-example");
        var originalSiteCipher = legacyEntry.Site;

        await using (var context = database.CreateContext())
        {
            context.Users.Add(legacyEntry);
            await context.SaveChangesAsync();
        }

        var session = new VaultSession();
        var service = CreateUnlockService(database, session);

        Assert.Equal(VaultStatus.Existing, await service.GetStatusAsync());
        Assert.Equal(UnlockResult.InvalidPassword, await service.UnlockAsync("wrong-example"));
        Assert.False(session.IsUnlocked);
        Assert.False(File.Exists(database.DatabasePath + ".migrating.bak"));

        await using (var context = database.CreateContext())
        {
            var untouched = Assert.Single(context.Users.ToList());

            Assert.Equal(originalSiteCipher, untouched.Site);
            Assert.Empty(context.VaultMetadata.ToList());
        }

        Assert.Equal(UnlockResult.Success, await service.UnlockAsync("correct-example"));

        Assert.True(session.IsUnlocked);
        Assert.False(File.Exists(database.DatabasePath + ".migrating.bak"));

        await using (var context = database.CreateContext())
        {
            var migrated = Assert.Single(context.Users.ToList());

            Assert.True(VaultCrypto.IsV2(migrated.Site));
            Assert.Equal("GitHub", VaultCrypto.Decrypt(session.GetKey(), migrated.Site));
            Assert.Equal("alice", VaultCrypto.Decrypt(session.GetKey(), migrated.Login));
            Assert.Equal("secret", VaultCrypto.Decrypt(session.GetKey(), migrated.Password));
            Assert.Single(context.VaultMetadata.ToList());
        }

        // A second unlock uses the v2 metadata and does not re-migrate.
        var secondSession = new VaultSession();
        var secondService = CreateUnlockService(database, secondSession);

        Assert.Equal(UnlockResult.Success, await secondService.UnlockAsync("correct-example"));

        await using var finalContext = database.CreateContext();
        var stable = Assert.Single(finalContext.Users.ToList());

        Assert.Equal("GitHub", VaultCrypto.Decrypt(secondSession.GetKey(), stable.Site));
    }

    [Fact]
    public async Task UnlockService_StaleMigrationBackup_DoesNotBlockTheNextAttempt()
    {
        using var database = await TestDatabase.CreateAsync();
        var legacyEntry = CreateLegacyEntry("correct-example");

        await using (var context = database.CreateContext())
        {
            context.Users.Add(legacyEntry);
            await context.SaveChangesAsync();
        }

        await File.WriteAllTextAsync(database.DatabasePath + ".migrating.bak", "stale");

        var session = new VaultSession();
        var service = CreateUnlockService(database, session);

        Assert.Equal(UnlockResult.Success, await service.UnlockAsync("correct-example"));
        Assert.True(session.IsUnlocked);
        Assert.False(File.Exists(database.DatabasePath + ".migrating.bak"));
    }

    [Fact]
    public async Task UnlockService_WrongPasswordOnV2Vault_LeavesDataReadable()
    {
        using var database = await TestDatabase.CreateAsync();
        var creator = new VaultSession();
        var createService = CreateUnlockService(database, creator);

        Assert.Equal(UnlockResult.Success, await createService.UnlockAsync("correct-example"));

        var user = new User("Site", "login", "secret");
        await using (var context = database.CreateContext())
        {
            context.Users.Add(new UserEncryptorService(creator, new VaultCipherService()).EncryptUser(user));
            await context.SaveChangesAsync();
        }

        var session = new VaultSession();
        var service = CreateUnlockService(database, session);

        Assert.Equal(UnlockResult.InvalidPassword, await service.UnlockAsync("wrong-example"));
        Assert.Equal(UnlockResult.Success, await service.UnlockAsync("correct-example"));

        await using var finalContext = database.CreateContext();
        var stored = Assert.Single(finalContext.Users.ToList());

        Assert.Equal("Site", VaultCrypto.Decrypt(session.GetKey(), stored.Site));
        Assert.Equal("secret", VaultCrypto.Decrypt(session.GetKey(), stored.Password));
    }

    private static MainViewModel CreateViewModel(TestDatabase database, IVaultSession session)
    {
        var settings = new AppSettingsService(
            new AppSettingsStore(Path.Combine(database.DirectoryPath, "settings")));
        var themeSettings = new ThemeSettingsViewModel(
            Mock.Of<IThemeService>(),
            new ColorThemeStore(Path.Combine(database.DirectoryPath, "themes")));
        var securitySettings = new SecuritySettingsViewModel(
            settings,
            Mock.Of<IVaultRekeyService>(),
            Mock.Of<IVaultMaintenanceService>(),
            Mock.Of<IFilePickerService>());

        return new MainViewModel(
            new VaultEntryService(
                database.CreateUserRepository(),
                new UserEncryptorService(session, new VaultCipherService()),
                TimeProvider.System),
            Mock.Of<IClipboardService>(),
            TimeProvider.System,
            settings,
            themeSettings,
            securitySettings,
            TestAppInfo.Create());
    }

    private static UnlockService CreateUnlockService(TestDatabase database, IVaultSession session)
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
            session,
            TimeProvider.System);
    }

    private static VaultSession CreateUnlockedSession(string password)
    {
        var session = new VaultSession();
        session.Unlock(VaultCrypto.DeriveKey(password, VaultCrypto.CreateSalt(), 1000));

        return session;
    }

    private static User CreateLegacyEntry(string password)
    {
        return new User(
            LegacyVaultCrypto.Encrypt("GitHub", password),
            LegacyVaultCrypto.Encrypt("alice", password),
            LegacyVaultCrypto.Encrypt("secret", password));
    }
}
