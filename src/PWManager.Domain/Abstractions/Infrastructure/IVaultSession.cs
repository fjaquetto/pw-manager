namespace PWManager.Domain.Abstractions.Infrastructure;

public interface IVaultSession
{
    bool IsUnlocked { get; }

    void Unlock(byte[] key);

    void Lock();

    byte[] GetKey();
}
