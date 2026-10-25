using System.Security.Cryptography;
using System.Text;

namespace PWManager.Infra.Security;

// v2 vault cryptography: PBKDF2-SHA256 derives the key once per session and
// AES-GCM authenticates every field, so tampered ciphertext is rejected.
public static class VaultCrypto
{
    public const string Prefix = "v2:";

    public const int SaltBytes = 16;

    private const int KeyBytes = 32;
    private const int NonceBytes = 12;
    private const int TagBytes = 16;
    private const string VerifierPlaintext = "PWManager vault verifier";

    public static byte[] CreateSalt() => RandomNumberGenerator.GetBytes(SaltBytes);

    public static byte[] DeriveKey(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeyBytes);

    public static string Encrypt(byte[] key, string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagBytes];

        using (var aes = new AesGcm(key, TagBytes))
        {
            aes.Encrypt(nonce, plainBytes, cipher, tag);
        }

        var payload = new byte[cipher.Length + tag.Length];
        Buffer.BlockCopy(cipher, 0, payload, 0, cipher.Length);
        Buffer.BlockCopy(tag, 0, payload, cipher.Length, tag.Length);

        return $"{Prefix}{Convert.ToBase64String(nonce)}:{Convert.ToBase64String(payload)}";
    }

    public static string Decrypt(byte[] key, string ciphertext)
    {
        if (!IsV2(ciphertext))
        {
            throw new CryptographicException("The ciphertext is not in the v2 format.");
        }

        var parts = ciphertext[Prefix.Length..].Split(':');

        if (parts.Length != 2)
        {
            throw new CryptographicException("The ciphertext is malformed.");
        }

        var nonce = Convert.FromBase64String(parts[0]);
        var payload = Convert.FromBase64String(parts[1]);

        if (nonce.Length != NonceBytes || payload.Length < TagBytes)
        {
            throw new CryptographicException("The ciphertext is malformed.");
        }

        var cipher = new byte[payload.Length - TagBytes];
        var tag = new byte[TagBytes];
        Buffer.BlockCopy(payload, 0, cipher, 0, cipher.Length);
        Buffer.BlockCopy(payload, cipher.Length, tag, 0, TagBytes);
        var plain = new byte[cipher.Length];

        using (var aes = new AesGcm(key, TagBytes))
        {
            aes.Decrypt(nonce, cipher, tag, plain);
        }

        return Encoding.UTF8.GetString(plain);
    }

    public static bool IsV2(string value) => value.StartsWith(Prefix, StringComparison.Ordinal);

    public static string CreateVerifier(byte[] key) => Encrypt(key, VerifierPlaintext);

    public static bool VerifyKey(byte[] key, string verifier)
    {
        try
        {
            return Decrypt(key, verifier) == VerifierPlaintext;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
