using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PWManager.Infra.Persistence;

// Design-time commands must never touch the user's real vault. Point them at a
// dedicated development database instead.
public sealed class PWDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PWDbContext>
{
    private const string DatabasePathVariable = "PWMANAGER_DB_PATH";

    public PWDbContext CreateDbContext(string[] args)
    {
        var databasePath = Environment.GetEnvironmentVariable(DatabasePathVariable);

        if (string.IsNullOrWhiteSpace(databasePath))
        {
            databasePath = Path.Combine(Directory.GetCurrentDirectory(), ".dev", "pwmanager.db");
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var options = new DbContextOptionsBuilder<PWDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString())
            .Options;

        return new PWDbContext(options);
    }
}
