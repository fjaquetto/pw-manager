using PWManager.Domain.Vault;

namespace PWManager.Domain.Abstractions.Infrastructure;

public interface IVaultArchiveReader
{
    Task<VaultArchiveSnapshot> ReadAsync(string databasePath);
}
