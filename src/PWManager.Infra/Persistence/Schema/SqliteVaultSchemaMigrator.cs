using Microsoft.EntityFrameworkCore;
using PWManager.Domain.Abstractions.Infrastructure;

namespace PWManager.Infra.Persistence.Schema;

public sealed class SqliteVaultSchemaMigrator(IDbContextFactory<PWDbContext> contextFactory) : IVaultSchemaMigrator
{
    public async Task MigrateAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        await LegacyVaultBaseline.ApplyAsync(context);
        await context.Database.MigrateAsync();
    }
}
