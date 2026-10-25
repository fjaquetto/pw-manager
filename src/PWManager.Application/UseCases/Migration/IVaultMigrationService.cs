using PWManager.Domain.Vault;

namespace PWManager.Application.UseCases.Migration;

public interface IVaultMigrationService
{
    Task<VaultMigrationResult> MigrateLegacyAsync(string legacyPassword, byte[] newKey, VaultMetadata newMetadata);

    Task<VaultMigrationResult> ReencryptAsync(byte[] currentKey, byte[] newKey, VaultMetadata updatedMetadata);
}
