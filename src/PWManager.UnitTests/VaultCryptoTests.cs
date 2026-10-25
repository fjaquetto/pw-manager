using System.Security.Cryptography;
using PWManager.Infra.Security;

namespace PWManager.UnitTests;

public class VaultCryptoTests
{
    private static readonly byte[] Salt = Convert.FromBase64String("AAECAwQFBgcICQoLDA0ODw==");

    [Theory]
    [InlineData("")]
    [InlineData("senha com acentos çãõ")]
    [InlineData("emoji 😀 and symbols !@#$%^&*()")]
    [InlineData("line\nbreak")]
    public void RoundTrip_PreservesAnyText(string plaintext)
    {
        var key = VaultCrypto.DeriveKey("password", Salt, 1000);

        Assert.Equal(plaintext, VaultCrypto.Decrypt(key, VaultCrypto.Encrypt(key, plaintext)));
    }

    [Fact]
    public void Encrypt_IsVersionedAndUsesAFreshNonce()
    {
        var key = VaultCrypto.DeriveKey("password", Salt, 1000);

        var first = VaultCrypto.Encrypt(key, "same");
        var second = VaultCrypto.Encrypt(key, "same");

        Assert.StartsWith("v2:", first);
        Assert.NotEqual(first, second);
        Assert.False(VaultCrypto.IsV2("c2FsdA==:aXY=:Y2lwaGVy"));
    }

    [Fact]
    public void Decrypt_RejectsTamperedCiphertext()
    {
        var key = VaultCrypto.DeriveKey("password", Salt, 1000);
        var ciphertext = VaultCrypto.Encrypt(key, "secret");
        var parts = ciphertext[3..].Split(':');
        var payload = Convert.FromBase64String(parts[1]);
        payload[0] ^= 0xFF;
        var tampered = $"v2:{parts[0]}:{Convert.ToBase64String(payload)}";

        Assert.ThrowsAny<CryptographicException>(() => VaultCrypto.Decrypt(key, tampered));
    }

    [Fact]
    public void Decrypt_RejectsTheWrongKey()
    {
        var key = VaultCrypto.DeriveKey("password", Salt, 1000);
        var otherKey = VaultCrypto.DeriveKey("other-password", Salt, 1000);
        var ciphertext = VaultCrypto.Encrypt(key, "secret");

        Assert.ThrowsAny<CryptographicException>(() => VaultCrypto.Decrypt(otherKey, ciphertext));
    }

    [Fact]
    public void Verifier_OnlyAcceptsTheMatchingKey()
    {
        var key = VaultCrypto.DeriveKey("password", Salt, 1000);
        var otherKey = VaultCrypto.DeriveKey("other-password", Salt, 1000);

        Assert.True(VaultCrypto.VerifyKey(key, VaultCrypto.CreateVerifier(key)));
        Assert.False(VaultCrypto.VerifyKey(otherKey, VaultCrypto.CreateVerifier(key)));
        Assert.False(VaultCrypto.VerifyKey(key, "garbage"));
    }

    [Fact]
    public void DeriveKey_IsDeterministicAndSaltSensitive()
    {
        var first = VaultCrypto.DeriveKey("password", Salt, 1000);
        var second = VaultCrypto.DeriveKey("password", Salt, 1000);
        var otherSalt = VaultCrypto.DeriveKey("password", VaultCrypto.CreateSalt(), 1000);

        Assert.Equal(first, second);
        Assert.NotEqual(first, otherSalt);
        Assert.Equal(32, first.Length);
    }
}
