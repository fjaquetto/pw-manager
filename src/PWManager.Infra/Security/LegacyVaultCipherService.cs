using PWManager.Domain.Abstractions.Infrastructure;

namespace PWManager.Infra.Security;

public sealed class LegacyVaultCipherService : ILegacyVaultCipher
{
    public string Decrypt(string ciphertext, string masterPassword) =>
        LegacyVaultCrypto.Decrypt(ciphertext, masterPassword);
}
