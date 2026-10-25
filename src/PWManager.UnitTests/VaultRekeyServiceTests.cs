using PWManager.Application.UseCases.Migration;
using PWManager.Application.UseCases.Rekey;
using PWManager.Domain.Abstractions.Infrastructure;
using PWManager.Domain.Vault;
using PWManager.Infra.Security;

namespace PWManager.UnitTests;

public class VaultRekeyServiceTests
{
    private const int TestIterations = 1000;

    [Fact]
    public async Task Change_ReencryptsEverythingAndSwapsTheSessionKey()
    {
        using var database = await TestDatabase.CreateAsync();
        var (service, session, oldKey, oldSalt) = await CreateVaultAsync(database, "current-password");

        var result = await service.ChangeMasterPasswordAsync("current-password", "new-password");

        Assert.Equal(RekeyResult.Success, result);
        Assert.False(File.Exists(database.DatabasePath + ".rekey.bak"));

        await using var context = database.CreateContext();
        var metadata = context.VaultMetadata.Single();

        Assert.NotEqual(oldSalt, metadata.KdfSalt);
        Assert.Equal(VaultMetadata.DefaultIterations, metadata.KdfIterations);
        Assert.True(VaultCrypto.VerifyKey(session.GetKey(), metadata.Verifier));

        var entry = context.Users.Single();

        Assert.Equal("GitHub", VaultCrypto.Decrypt(session.GetKey(), entry.Site));
        Assert.Equal("alice", VaultCrypto.Decrypt(session.GetKey(), entry.Login));
        Assert.Equal("secret", VaultCrypto.Decrypt(session.GetKey(), entry.Password));
        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() => VaultCrypto.Decrypt(oldKey, entry.Site));
    }

    [Fact]
    public async Task Change_StaleRekeyBackup_DoesNotBlockTheOperation()
    {
        using var database = await TestDatabase.CreateAsync();
        var (service, _, _, _) = await CreateVaultAsync(database, "current-password");
        await File.WriteAllTextAsync(database.DatabasePath + ".rekey.bak", "stale");

        var result = await service.ChangeMasterPasswordAsync("current-password", "new-password");

        Assert.Equal(RekeyResult.Success, result);
        Assert.False(File.Exists(database.DatabasePath + ".rekey.bak"));
    }

    [Fact]
    public async Task Change_RejectsTheWrongCurrentPassword()
    {
        using var database = await TestDatabase.CreateAsync();
        var (service, session, oldKey, _) = await CreateVaultAsync(database, "current-password");

        var result = await service.ChangeMasterPasswordAsync("wrong-password", "new-password");

        Assert.Equal(RekeyResult.InvalidCurrentPassword, result);

        await using var context = database.CreateContext();
        var entry = context.Users.Single();

        Assert.Equal("GitHub", VaultCrypto.Decrypt(oldKey, entry.Site));
        Assert.True(VaultCrypto.VerifyKey(oldKey, context.VaultMetadata.Single().Verifier));
        Assert.True(session.IsUnlocked);
    }

    [Fact]
    public async Task Change_WithoutMetadata_IsUnavailable()
    {
        using var database = await TestDatabase.CreateAsync();
        var service = new VaultRekeyService(
            database.CreateMetadataRepository(),
            new VaultKeyService(),
            CreateMigrationService(database),
            new VaultSession());

        Assert.Equal(RekeyResult.Unavailable, await service.ChangeMasterPasswordAsync("a", "b"));
    }

    [Fact]
    public async Task Change_WithCorruptData_RollsBackAndKeepsTheOldState()
    {
        using var database = await TestDatabase.CreateAsync();
        var (service, _, oldKey, _) = await CreateVaultAsync(database, "current-password");

        await using (var context = database.CreateContext())
        {
            context.Users.Single().Login = "corrupt-not-v2";
            await context.SaveChangesAsync();
        }

        var result = await service.ChangeMasterPasswordAsync("current-password", "new-password");

        Assert.Equal(RekeyResult.Unavailable, result);

        await using (var context = database.CreateContext())
        {
            var entry = context.Users.Single();

            Assert.Equal("corrupt-not-v2", entry.Login);
            Assert.Equal("GitHub", VaultCrypto.Decrypt(oldKey, entry.Site));
            Assert.True(VaultCrypto.VerifyKey(oldKey, context.VaultMetadata.Single().Verifier));
        }
    }

    private static async Task<(VaultRekeyService Service, VaultSession Session, byte[] OldKey, string OldSalt)> CreateVaultAsync(
        TestDatabase database,
        string password)
    {
        var session = new VaultSession();
        var salt = VaultCrypto.CreateSalt();
        var oldSalt = Convert.ToBase64String(salt);
        var oldKey = VaultCrypto.DeriveKey(password, salt, TestIterations);
        session.Unlock(oldKey);

        await using (var context = database.CreateContext())
        {
            context.VaultMetadata.Add(new VaultMetadata
            {
                Id = 1,
                KdfSalt = oldSalt,
                KdfIterations = TestIterations,
                Verifier = VaultCrypto.CreateVerifier(oldKey)
            });
            context.Users.Add(new UserEncryptorService(session, new VaultCipherService()).EncryptUser(new User("GitHub", "alice", "secret")));
            await context.SaveChangesAsync();
        }

        var service = new VaultRekeyService(
            database.CreateMetadataRepository(),
            new VaultKeyService(),
            CreateMigrationService(database),
            session);

        return (service, session, oldKey, oldSalt);
    }

    private static VaultMigrationService CreateMigrationService(TestDatabase database)
    {
        return new VaultMigrationService(
            database.CreateMigrationRepository(),
            database.CreateBackupService(),
            new VaultCipherService(),
            new LegacyVaultCipherService());
    }
}
