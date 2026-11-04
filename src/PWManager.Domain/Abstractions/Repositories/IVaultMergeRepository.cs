using PWManager.Domain.Vault;

namespace PWManager.Domain.Abstractions.Repositories;

public interface IVaultMergeRepository
{
    Task ApplyAsync(IReadOnlyList<User> inserts, IReadOnlyList<User> updates);
}
