namespace PWManager.Application.UseCases.Import;

public sealed record VaultImportResult(
    VaultImportStatus Status,
    int Added = 0,
    int Updated = 0,
    int Unchanged = 0,
    int Skipped = 0,
    int Conflicts = 0,
    string? SafetyCopyPath = null);
