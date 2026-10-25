
namespace PWManager.Application.UseCases.Unlock;

public interface IUnlockService
{
    Task<VaultStatus> GetStatusAsync();

    Task<UnlockResult> UnlockAsync(string password);
}
