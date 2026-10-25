using PWManager.Application.UseCases.Unlock;

namespace PWManager.Application.UseCases.Bootstrapping;

public interface IVaultBootstrapper
{
    Task<VaultStatus> InitializeAsync();
}
