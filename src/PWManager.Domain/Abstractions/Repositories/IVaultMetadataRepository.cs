using PWManager.Domain.Vault;

namespace PWManager.Domain.Abstractions.Repositories;

public interface IVaultMetadataRepository
{
    Task<bool> AnyAsync();

    Task<VaultMetadata?> GetAsync();

    Task AddAsync(VaultMetadata metadata);
}
