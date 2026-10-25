namespace PWManager.Domain.Vault;

public sealed record VaultMigrationState(VaultMetadata? Metadata, IReadOnlyList<User> Entries);
