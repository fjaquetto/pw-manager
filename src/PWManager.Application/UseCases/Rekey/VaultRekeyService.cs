using System.Security.Cryptography;
using PWManager.Application.UseCases.Migration;
using PWManager.Domain.Abstractions.Infrastructure;
using PWManager.Domain.Abstractions.Repositories;
using PWManager.Domain.Vault;

namespace PWManager.Application.UseCases.Rekey;

public sealed class VaultRekeyService(
    IVaultMetadataRepository metadataRepository,
    IVaultKeyService keyService,
    IVaultMigrationService migrationService,
    IVaultSession session) : IVaultRekeyService
{
    public async Task<RekeyResult> ChangeMasterPasswordAsync(string currentPassword, string newPassword)
    {
        byte[]? currentKey = null;
        byte[]? newKey = null;

        try
        {
            var metadata = await metadataRepository.GetAsync();

            if (metadata is null)
            {
                return RekeyResult.Unavailable;
            }

            var currentSalt = Convert.FromBase64String(metadata.KdfSalt);
            currentKey = keyService.DeriveKey(currentPassword, currentSalt, metadata.KdfIterations);

            if (!keyService.VerifyKey(currentKey, metadata.Verifier))
            {
                return RekeyResult.InvalidCurrentPassword;
            }

            var newSalt = keyService.CreateSalt();
            newKey = keyService.DeriveKey(newPassword, newSalt, VaultMetadata.DefaultIterations);
            var updatedMetadata = new VaultMetadata
            {
                Id = metadata.Id,
                FormatVersion = VaultMetadata.CurrentFormatVersion,
                Kdf = VaultMetadata.Pbkdf2Sha256,
                KdfIterations = VaultMetadata.DefaultIterations,
                KdfSalt = Convert.ToBase64String(newSalt),
                Verifier = keyService.CreateVerifier(newKey),
                CreatedAt = metadata.CreatedAt
            };

            var result = await migrationService.ReencryptAsync(currentKey, newKey, updatedMetadata);

            if (result != VaultMigrationResult.Success)
            {
                return RekeyResult.Unavailable;
            }

            session.Unlock(newKey);
            newKey = null;

            return RekeyResult.Success;
        }
        catch (Exception)
        {
            return RekeyResult.Unavailable;
        }
        finally
        {
            Zero(currentKey);
            Zero(newKey);
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
