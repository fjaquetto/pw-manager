using PWManager.Domain.Vault;

namespace PWManager.Domain.Abstractions.Repositories;

public interface IVaultMigrationRepository
{
    Task<VaultMigrationState> LoadAsync();

    Task SaveAsync(IReadOnlyList<User> entries, VaultMetadata metadata);
}
