namespace PWManager.Domain.Abstractions.Infrastructure;

public interface IVaultSchemaMigrator
{
    Task MigrateAsync();

    Task MigrateAsync(string databasePath);
}
