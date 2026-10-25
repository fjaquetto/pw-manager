using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PWManager.Infra.Persistence.Migrations;

namespace PWManager.Infra.Persistence.Schema;

// Old releases used a mix of migrations and EnsureCreated. A database created
// by EnsureCreated has the current schema but no migration history, so applying
// every migration would try to recreate tables that already exist.
internal static class LegacyVaultBaseline
{
    public static async Task ApplyAsync(PWDbContext context)
    {
        if (!await TableExistsAsync(context, "User"))
        {
            return;
        }

        var history = context.GetService<IHistoryRepository>();
        var applied = history.Exists()
            ? (await context.Database.GetAppliedMigrationsAsync()).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        var migrations = context.GetService<IMigrationsAssembly>();
        var productVersion = typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "10.0.0";
        var alreadyPresent = new List<string>();

        var hasLastUpdated = await ColumnExistsAsync(context, "User", "LastUpdated");
        var hasIsActive = await ColumnExistsAsync(context, "User", "IsActive");

        AddIfKnown(alreadyPresent, migrations, nameof(InitialCreate));

        if (hasLastUpdated)
        {
            AddIfKnown(alreadyPresent, migrations, nameof(AddLastUpdatedColumn));
        }

        if (!hasIsActive)
        {
            AddIfKnown(alreadyPresent, migrations, nameof(RemoveIsActiveColumn));
        }

        if (await TableExistsAsync(context, "VaultMetadata"))
        {
            AddIfKnown(alreadyPresent, migrations, nameof(AddVaultMetadata));
        }

        var missing = alreadyPresent
            .Where(migrationId => !applied.Contains(migrationId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (missing.Length == 0)
        {
            return;
        }

        if (!history.Exists())
        {
            await context.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript());
        }

        foreach (var migrationId in missing)
        {
            await context.Database.ExecuteSqlRawAsync(
                history.GetInsertScript(new HistoryRow(migrationId, productVersion)));
        }
    }

    private static void AddIfKnown(List<string> migrationIds, IMigrationsAssembly migrations, string migrationName)
    {
        var migration = migrations.Migrations
            .FirstOrDefault(candidate => candidate.Value.Name == migrationName);

        if (migration.Key is not null)
        {
            migrationIds.Add(migration.Key);
        }
    }

    private static async Task<bool> TableExistsAsync(PWDbContext context, string tableName)
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "$name";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            return Convert.ToInt64(await command.ExecuteScalarAsync()) > 0;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> ColumnExistsAsync(PWDbContext context, string tableName, string columnName)
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{tableName}\");";

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
