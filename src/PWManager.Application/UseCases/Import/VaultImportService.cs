using System.Security.Cryptography;
using PWManager.Application.UseCases.VaultEntries;
using PWManager.Domain.Abstractions.Infrastructure;
using PWManager.Domain.Abstractions.Repositories;
using PWManager.Domain.Vault;

namespace PWManager.Application.UseCases.Import;

public sealed class VaultImportService(
    IVaultBackupService backupService,
    IVaultSchemaMigrator schemaMigrator,
    IVaultArchiveReader archiveReader,
    IVaultKeyService keyService,
    IVaultCipher cipher,
    ILegacyVaultCipher legacyCipher,
    IVaultEntryService vaultEntries,
    IUserEncryptorService encryptor,
    IVaultMergeRepository mergeRepository) : IVaultImportService
{
    public Task<VaultImportResult> ImportAsync(string sourcePath, string sourcePassword) =>
        Task.Run(() => ImportCoreAsync(sourcePath, sourcePassword));

    private async Task<VaultImportResult> ImportCoreAsync(string sourcePath, string sourcePassword)
    {
        string? stagedPath = null;
        byte[]? sourceKey = null;

        try
        {
            stagedPath = await backupService.StageRestoreAsync(sourcePath);
            await schemaMigrator.MigrateAsync(stagedPath);

            var snapshot = await archiveReader.ReadAsync(stagedPath);
            var sourceEntries = new List<User>();

            if (snapshot.Metadata is not null)
            {
                if (!IsSupported(snapshot.Metadata) || string.IsNullOrEmpty(sourcePassword))
                {
                    return new VaultImportResult(VaultImportStatus.Unavailable);
                }

                var salt = Convert.FromBase64String(snapshot.Metadata.KdfSalt);
                sourceKey = keyService.DeriveKey(sourcePassword, salt, snapshot.Metadata.KdfIterations);

                if (!keyService.VerifyKey(sourceKey, snapshot.Metadata.Verifier))
                {
                    return new VaultImportResult(VaultImportStatus.InvalidPassword);
                }

                foreach (var entry in snapshot.Entries)
                {
                    sourceEntries.Add(new User
                    {
                        Id = entry.Id,
                        Site = cipher.Decrypt(sourceKey, entry.Site),
                        Login = cipher.Decrypt(sourceKey, entry.Login),
                        Password = cipher.Decrypt(sourceKey, entry.Password),
                        CreationDate = entry.CreationDate,
                        LastUpdated = entry.LastUpdated
                    });
                }
            }
            else
            {
                if (string.IsNullOrEmpty(sourcePassword))
                {
                    return new VaultImportResult(VaultImportStatus.InvalidPassword);
                }

                foreach (var entry in snapshot.Entries)
                {
                    if (!TryDecryptLegacy(entry, sourcePassword, out var decrypted))
                    {
                        return new VaultImportResult(VaultImportStatus.InvalidPassword);
                    }

                    sourceEntries.Add(decrypted);
                }
            }

            var currentEntries = await vaultEntries.LoadAsync();
            var currentById = currentEntries.ToDictionary(entry => entry.Id);
            var inserts = new List<User>();
            var updates = new List<User>();
            var unchanged = 0;
            var skipped = 0;
            var conflicts = 0;

            foreach (var source in sourceEntries)
            {
                if (!currentById.TryGetValue(source.Id, out var current))
                {
                    inserts.Add(EncryptForDestination(source, source.CreationDate, source.LastUpdated));
                    continue;
                }

                if (HasSameContent(source, current))
                {
                    unchanged++;
                    continue;
                }

                if (source.LastUpdated > current.LastUpdated)
                {
                    updates.Add(EncryptForDestination(source, current.CreationDate, source.LastUpdated));
                }
                else if (current.LastUpdated > source.LastUpdated)
                {
                    skipped++;
                }
                else
                {
                    conflicts++;
                }
            }

            if (inserts.Count == 0 && updates.Count == 0)
            {
                return new VaultImportResult(
                    VaultImportStatus.Success,
                    Added: 0,
                    Updated: 0,
                    Unchanged: unchanged,
                    Skipped: skipped,
                    Conflicts: conflicts);
            }

            var safetyCopy = $"{backupService.DatabasePath}.before-import-{DateTime.Now:yyyyMMddHHmmss}.bak";
            await backupService.BackupAsync(safetyCopy);
            await mergeRepository.ApplyAsync(inserts, updates);

            return new VaultImportResult(
                VaultImportStatus.Success,
                inserts.Count,
                updates.Count,
                unchanged,
                skipped,
                conflicts,
                safetyCopy);
        }
        catch (CryptographicException)
        {
            return new VaultImportResult(VaultImportStatus.InvalidPassword);
        }
        catch (FormatException)
        {
            return new VaultImportResult(VaultImportStatus.Unavailable);
        }
        catch (Exception)
        {
            return new VaultImportResult(VaultImportStatus.Unavailable);
        }
        finally
        {
            if (sourceKey is not null)
            {
                CryptographicOperations.ZeroMemory(sourceKey);
            }

            if (stagedPath is not null)
            {
                await backupService.DiscardStagedAsync(stagedPath);
            }
        }
    }

    private static bool IsSupported(VaultMetadata metadata) =>
        metadata.FormatVersion == VaultMetadata.CurrentFormatVersion
        && string.Equals(metadata.Kdf, VaultMetadata.Pbkdf2Sha256, StringComparison.Ordinal)
        && metadata.KdfIterations is >= 100_000 and <= 5_000_000
        && metadata.KdfSalt.Length > 0
        && metadata.Verifier.Length > 0;

    private bool TryDecryptLegacy(User entry, string password, out User decrypted)
    {
        decrypted = new User();

        try
        {
            decrypted = new User
            {
                Id = entry.Id,
                Site = legacyCipher.Decrypt(entry.Site, password),
                Login = legacyCipher.Decrypt(entry.Login, password),
                Password = legacyCipher.Decrypt(entry.Password, password),
                CreationDate = entry.CreationDate,
                LastUpdated = entry.LastUpdated
            };

            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private User EncryptForDestination(User source, DateTime creationDate, DateTime lastUpdated)
    {
        return encryptor.EncryptUser(new User
        {
            Id = source.Id,
            Site = source.Site,
            Login = source.Login,
            Password = source.Password,
            CreationDate = creationDate,
            LastUpdated = lastUpdated
        });
    }

    private static bool HasSameContent(User left, User right) =>
        string.Equals(left.Site, right.Site, StringComparison.Ordinal)
        && string.Equals(left.Login, right.Login, StringComparison.Ordinal)
        && string.Equals(left.Password, right.Password, StringComparison.Ordinal);
}
