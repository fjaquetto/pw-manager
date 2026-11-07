using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PWManager.Domain.Abstractions.Infrastructure;
using PWManager.Domain.Vault;

namespace PWManager.Infra.Persistence.Archives;

public sealed class VaultArchiveReader : IVaultArchiveReader
{
    public async Task<VaultArchiveSnapshot> ReadAsync(string databasePath)
    {
        var options = new DbContextOptionsBuilder<PWDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString())
            .Options;

        await using var context = new PWDbContext(options);
        var metadata = await context.VaultMetadata.AsNoTracking().FirstOrDefaultAsync();
        var entries = await context.Users.AsNoTracking().ToListAsync();

        return new VaultArchiveSnapshot(metadata, entries);
    }
}
