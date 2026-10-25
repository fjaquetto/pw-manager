using PWManager.Application.UseCases.Unlock;

namespace PWManager.UITests.Fakes;

internal sealed class UnlockStub : IUnlockService
{
    public int Calls { get; private set; }

    public UnlockResult Result { get; set; } = UnlockResult.InvalidPassword;

    public VaultStatus Status { get; set; } = VaultStatus.Existing;

    public TaskCompletionSource<UnlockResult>? Pending { get; set; }

    public Task<VaultStatus> GetStatusAsync() => Task.FromResult(Status);

    public Task<UnlockResult> UnlockAsync(string password)
    {
        Calls++;

        return Pending?.Task ?? Task.FromResult(Result);
    }
}
