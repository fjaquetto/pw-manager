namespace PWManager.Domain.Abstractions.Infrastructure;

public interface IVaultCipher
{
    string Encrypt(byte[] key, string plaintext);

    string Decrypt(byte[] key, string ciphertext);
}
