using System.Security.Cryptography;
using PWManager.Domain.Abstractions.Infrastructure;

namespace PWManager.Infra.Security;

// Locking zeroes the key material so it does not linger in managed memory.
public sealed class VaultSession : IVaultSession
{
    private byte[]? _key;

    public bool IsUnlocked => _key is not null;

    public void Unlock(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        Lock();
        _key = key;
    }

    public void Lock()
    {
        if (_key is null)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(_key);
        _key = null;
    }

    public byte[] GetKey() => _key ?? throw new InvalidOperationException("The vault is locked.");
}
