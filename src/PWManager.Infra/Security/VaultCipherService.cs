using PWManager.Domain.Abstractions.Infrastructure;

namespace PWManager.Infra.Security;

public sealed class VaultCipherService : IVaultCipher
{
    public string Encrypt(byte[] key, string plaintext) => VaultCrypto.Encrypt(key, plaintext);

    public string Decrypt(byte[] key, string ciphertext) => VaultCrypto.Decrypt(key, ciphertext);
}
