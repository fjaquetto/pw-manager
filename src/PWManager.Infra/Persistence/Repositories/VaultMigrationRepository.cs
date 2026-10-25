using Microsoft.EntityFrameworkCore;
using PWManager.Domain.Abstractions.Repositories;
using PWManager.Domain.Vault;
using PWManager.Infra.Persistence;

namespace PWManager.Infra.Persistence.Repositories;

public sealed class VaultMigrationRepository(IDbContextFactory<PWDbContext> contextFactory) : IVaultMigrationRepository
{
    public async Task<VaultMigrationState> LoadAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var metadata = await context.VaultMetadata.AsNoTracking().FirstOrDefaultAsync();
        var entries = await context.Users.AsNoTracking().ToListAsync();

        return new VaultMigrationState(metadata, entries);
    }

    public async Task SaveAsync(IReadOnlyList<User> entries, VaultMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(metadata);

        await using var context = await contextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();

        context.Users.UpdateRange(entries);

        if (await context.VaultMetadata.AnyAsync())
        {
            context.VaultMetadata.Update(metadata);
        }
        else
        {
            await context.VaultMetadata.AddAsync(metadata);
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
