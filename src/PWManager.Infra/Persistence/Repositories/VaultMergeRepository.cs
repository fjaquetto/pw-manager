using Microsoft.EntityFrameworkCore;
using PWManager.Domain.Abstractions.Repositories;
using PWManager.Domain.Vault;
using PWManager.Infra.Persistence;

namespace PWManager.Infra.Persistence.Repositories;

public sealed class VaultMergeRepository(IDbContextFactory<PWDbContext> contextFactory) : IVaultMergeRepository
{
    public async Task ApplyAsync(IReadOnlyList<User> inserts, IReadOnlyList<User> updates)
    {
        ArgumentNullException.ThrowIfNull(inserts);
        ArgumentNullException.ThrowIfNull(updates);

        await using var context = await contextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();

        if (inserts.Count > 0)
        {
            await context.Users.AddRangeAsync(inserts);
        }

        if (updates.Count > 0)
        {
            context.Users.UpdateRange(updates);
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
