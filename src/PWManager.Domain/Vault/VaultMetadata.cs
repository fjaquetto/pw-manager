namespace PWManager.Domain.Vault;

// Salt and verifier only: never contains secrets. Stored inside the database so
// it travels with backups.
public class VaultMetadata
{
    public const int CurrentFormatVersion = 2;

    public const string Pbkdf2Sha256 = "PBKDF2-SHA256";

    public const int DefaultIterations = 600_000;

    public int Id { get; set; } = 1;

    public int FormatVersion { get; set; } = CurrentFormatVersion;

    public string Kdf { get; set; } = Pbkdf2Sha256;

    public int KdfIterations { get; set; } = DefaultIterations;

    public string KdfSalt { get; set; } = string.Empty;

    public string Verifier { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
