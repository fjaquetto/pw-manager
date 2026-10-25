using PWManager.Domain.Vault;

namespace PWManager.Domain.Abstractions.Repositories;

public interface IUserRepository
{
    Task<IReadOnlyList<User>> GetAllAsync();

    Task<User?> GetUserByIdAsync(Guid id);

    Task<bool> AnyAsync();

    Task AddAsync(User user);

    Task UpdateAsync(User user);

    Task DeleteAsync(User user);
}
