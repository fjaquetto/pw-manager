using Microsoft.EntityFrameworkCore;
using PWManager.Domain.Abstractions.Repositories;
using PWManager.Domain.Vault;
using PWManager.Infra.Persistence;

namespace PWManager.Infra.Persistence.Repositories;

public sealed class UserRepository(IDbContextFactory<PWDbContext> contextFactory) : IUserRepository
{
    public async Task<IReadOnlyList<User>> GetAllAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.Users.AsNoTracking().ToListAsync();
    }

    public async Task<User?> GetUserByIdAsync(Guid id)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == id);
    }

    public async Task<bool> AnyAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.Users.AnyAsync();
    }

    public async Task AddAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using var context = await contextFactory.CreateDbContextAsync();
        context.Users.Update(user);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        await using var context = await contextFactory.CreateDbContextAsync();
        context.Users.Remove(user);
        await context.SaveChangesAsync();
    }
}
