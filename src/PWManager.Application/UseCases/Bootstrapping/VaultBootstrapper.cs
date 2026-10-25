using PWManager.Application.UseCases.Unlock;
using PWManager.Domain.Abstractions.Infrastructure;

namespace PWManager.Application.UseCases.Bootstrapping;

public sealed class VaultBootstrapper(
    IVaultSchemaMigrator schemaMigrator,
    IUnlockService unlockService) : IVaultBootstrapper
{
    // Microsoft.Data.Sqlite executes "async" commands synchronously, so this has
    // to own a worker thread or the shell would block while the status resolves.
    public Task<VaultStatus> InitializeAsync() => Task.Run(async () =>
    {
        await schemaMigrator.MigrateAsync();

        return await unlockService.GetStatusAsync();
    });
}
