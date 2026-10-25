namespace PWManager.Domain.Abstractions.Infrastructure;

public interface ILegacyVaultCipher
{
    string Decrypt(string ciphertext, string masterPassword);
}
