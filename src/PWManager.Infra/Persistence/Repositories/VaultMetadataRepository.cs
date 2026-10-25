using Microsoft.EntityFrameworkCore;
using PWManager.Domain.Abstractions.Repositories;
using PWManager.Domain.Vault;
using PWManager.Infra.Persistence;

namespace PWManager.Infra.Persistence.Repositories;

public sealed class VaultMetadataRepository(IDbContextFactory<PWDbContext> contextFactory) : IVaultMetadataRepository
{
    public async Task<bool> AnyAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.VaultMetadata.AnyAsync();
    }

    public async Task<VaultMetadata?> GetAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.VaultMetadata.AsNoTracking().FirstOrDefaultAsync();
    }

    public async Task AddAsync(VaultMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        await using var context = await contextFactory.CreateDbContextAsync();
        await context.VaultMetadata.AddAsync(metadata);
        await context.SaveChangesAsync();
    }
}
