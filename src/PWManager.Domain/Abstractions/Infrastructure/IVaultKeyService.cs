namespace PWManager.Domain.Abstractions.Infrastructure;

public interface IVaultKeyService
{
    byte[] CreateSalt();

    byte[] DeriveKey(string password, byte[] salt, int iterations);

    string CreateVerifier(byte[] key);

    bool VerifyKey(byte[] key, string verifier);
}
