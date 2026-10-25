using System.Security.Cryptography;

namespace PWManager.Infra.Security;

// The original per-field encryption: PBKDF2-SHA1 with 1000 iterations and AES-CBC.
// Kept only to read pre-v2 vaults; the derived keys are never retained.
public static class LegacyVaultCrypto
{
    private const int Iterations = 1000;

    public static string Encrypt(string text, string masterPassword)
    {
        byte[] iv = GenerateSalt(16);
        byte[] salt = GenerateSalt(16);
        byte[] key = GetKey(masterPassword, salt);
        byte[] array;

        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;

            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using (MemoryStream memoryStream = new())
            {
                using (CryptoStream cryptoStream = new(memoryStream, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter streamWriter = new(cryptoStream))
                    {
                        streamWriter.Write(text);
                    }

                    array = memoryStream.ToArray();
                }
            }
        }

        return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(iv) + ":" + Convert.ToBase64String(array);
    }

    public static string Decrypt(string cipherText, string masterPassword)
    {
        string[] parts = cipherText.Split(':');
        byte[] salt = Convert.FromBase64String(parts[0]);
        byte[] iv = Convert.FromBase64String(parts[1]);
        byte[] buffer = Convert.FromBase64String(parts[2]);

        byte[] key = GetKey(masterPassword, salt);

        using (Aes aes = Aes.Create())
        {
            aes.Key = key;
            aes.IV = iv;

            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            using (MemoryStream memoryStream = new(buffer))
            {
                using (CryptoStream cryptoStream = new(memoryStream, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader streamReader = new(cryptoStream))
                    {
                        return streamReader.ReadToEnd();
                    }
                }
            }
        }
    }

    private static byte[] GetKey(string masterPassword, byte[] salt, int keySize = 256)
    {
        // PBKDF2-HMAC-SHA1 with 1000 iterations: the exact derivation used by
        // the original format, required to read pre-v2 vaults.
        return Rfc2898DeriveBytes.Pbkdf2(masterPassword, salt, Iterations, HashAlgorithmName.SHA1, keySize / 8);
    }

    private static byte[] GenerateSalt(int size)
    {
        return RandomNumberGenerator.GetBytes(size);
    }
}
