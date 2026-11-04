namespace PWManager.Application.UseCases.Import;

public interface IVaultImportService
{
    Task<VaultImportResult> ImportAsync(string sourcePath, string sourcePassword);
}
