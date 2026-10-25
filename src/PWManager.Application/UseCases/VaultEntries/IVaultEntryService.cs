using PWManager.Domain.Vault;

namespace PWManager.Application.UseCases.VaultEntries;

public interface IVaultEntryService
{
    Task<IReadOnlyList<User>> LoadAsync();

    Task<User?> SaveAsync(Guid? id, string site, string login, string password);

    Task<bool> DeleteAsync(Guid id);
}
