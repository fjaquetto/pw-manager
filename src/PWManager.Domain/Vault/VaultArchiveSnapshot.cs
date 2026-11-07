namespace PWManager.Domain.Vault;

public sealed record VaultArchiveSnapshot(VaultMetadata? Metadata, IReadOnlyList<User> Entries);
