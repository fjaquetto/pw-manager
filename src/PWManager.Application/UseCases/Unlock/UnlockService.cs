using System.Security.Cryptography;
using PWManager.Application.UseCases.Migration;
using PWManager.Domain.Abstractions.Infrastructure;
using PWManager.Domain.Abstractions.Repositories;
using PWManager.Domain.Vault;

namespace PWManager.Application.UseCases.Unlock;

public sealed class UnlockService(
    IVaultMetadataRepository metadataRepository,
    IUserRepository userRepository,
    IVaultKeyService keyService,
    IVaultMigrationService migrationService,
    IVaultSession session,
    TimeProvider timeProvider) : IUnlockService
{
    public async Task<VaultStatus> GetStatusAsync()
    {
        if (await metadataRepository.AnyAsync())
        {
            return VaultStatus.Existing;
        }

        return await userRepository.AnyAsync() ? VaultStatus.Existing : VaultStatus.New;
    }

    public async Task<UnlockResult> UnlockAsync(string password)
    {
        try
        {
            var metadata = await metadataRepository.GetAsync();

            if (metadata is not null)
            {
                return UnlockExisting(metadata, password);
            }

            return await userRepository.AnyAsync()
                ? await MigrateLegacyVaultAsync(password)
                : await CreateVaultAsync(password);
        }
        catch (CryptographicException)
        {
            session.Lock();
            return UnlockResult.InvalidPassword;
        }
        catch (Exception)
        {
            session.Lock();
            return UnlockResult.Unavailable;
        }
    }

    private UnlockResult UnlockExisting(VaultMetadata metadata, string password)
    {
        var salt = Convert.FromBase64String(metadata.KdfSalt);
        byte[]? key = keyService.DeriveKey(password, salt, metadata.KdfIterations);

        try
        {
            if (!keyService.VerifyKey(key!, metadata.Verifier))
            {
                return UnlockResult.InvalidPassword;
            }

            session.Unlock(key);
            key = null;

            return UnlockResult.Success;
        }
        finally
        {
            Zero(key);
        }
    }

    private async Task<UnlockResult> CreateVaultAsync(string password)
    {
        var salt = keyService.CreateSalt();
        byte[]? key = keyService.DeriveKey(password, salt, VaultMetadata.DefaultIterations);

        try
        {
            var metadata = new VaultMetadata
            {
                Id = 1,
                KdfSalt = Convert.ToBase64String(salt),
                Verifier = keyService.CreateVerifier(key!),
                CreatedAt = timeProvider.GetLocalNow().DateTime
            };

            await metadataRepository.AddAsync(metadata);

            session.Unlock(key);
            key = null;

            return UnlockResult.Success;
        }
        finally
        {
            Zero(key);
        }
    }

    private async Task<UnlockResult> MigrateLegacyVaultAsync(string password)
    {
        var salt = keyService.CreateSalt();
        byte[]? key = keyService.DeriveKey(password, salt, VaultMetadata.DefaultIterations);

        try
        {
            var metadata = new VaultMetadata
            {
                Id = 1,
                KdfSalt = Convert.ToBase64String(salt),
                Verifier = keyService.CreateVerifier(key!),
                CreatedAt = timeProvider.GetLocalNow().DateTime
            };

            var result = await migrationService.MigrateLegacyAsync(password, key!, metadata);

            if (result == VaultMigrationResult.Success)
            {
                session.Unlock(key);
                key = null;

                return UnlockResult.Success;
            }

            session.Lock();

            return result == VaultMigrationResult.InvalidPassword
                ? UnlockResult.InvalidPassword
                : UnlockResult.Unavailable;
        }
        finally
        {
            Zero(key);
        }
    }

    private static void Zero(byte[]? key)
    {
        if (key is not null)
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }
}
