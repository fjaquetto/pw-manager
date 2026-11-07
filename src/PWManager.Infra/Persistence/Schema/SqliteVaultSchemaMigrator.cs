using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PWManager.Domain.Abstractions.Infrastructure;

namespace PWManager.Infra.Persistence.Schema;

public sealed class SqliteVaultSchemaMigrator(IDbContextFactory<PWDbContext> contextFactory) : IVaultSchemaMigrator
{
    public async Task MigrateAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        await MigrateAsync(context);
    }

    public async Task MigrateAsync(string databasePath)
    {
        var options = new DbContextOptionsBuilder<PWDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString())
            .Options;

        await using var context = new PWDbContext(options);
        await MigrateAsync(context);
    }

    private static async Task MigrateAsync(PWDbContext context)
    {
        await LegacyVaultBaseline.ApplyAsync(context);
        await context.Database.MigrateAsync();
    }
}
