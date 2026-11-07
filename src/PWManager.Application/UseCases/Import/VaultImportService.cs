using System.Globalization;
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
    IVaultMergeRepository mergeRepository,
    TimeProvider timeProvider) : IVaultImportService
{
    public Task<VaultImportResult> ImportAsync(string sourcePath, string sourcePassword) =>
        Task.Run(() => ImportCoreAsync(sourcePath, sourcePassword));

    private async Task<VaultImportResult> ImportCoreAsync(string sourcePath, string sourcePassword)
    {
        string? stagedPath = null;
        DecryptedSource? source = null;

        try
        {
            stagedPath = await backupService.StageRestoreAsync(sourcePath);
            await schemaMigrator.MigrateAsync(stagedPath);

            var snapshot = await archiveReader.ReadAsync(stagedPath);
            source = DecryptSource(snapshot, sourcePassword);

            if (source.Status != VaultImportStatus.Success)
            {
                return new VaultImportResult(source.Status);
            }

            var plan = await BuildMergePlanAsync(source.Entries);
            return await ApplyMergePlanAsync(plan);
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
            source?.Dispose();

            if (stagedPath is not null)
            {
                await backupService.DiscardStagedAsync(stagedPath);
            }
        }
    }

    private DecryptedSource DecryptSource(VaultArchiveSnapshot snapshot, string password)
    {
        return snapshot.Metadata is null
            ? DecryptLegacySource(snapshot.Entries, password)
            : DecryptCurrentSource(snapshot.Metadata, snapshot.Entries, password);
    }

    private DecryptedSource DecryptCurrentSource(
        VaultMetadata metadata,
        IReadOnlyList<User> entries,
        string password)
    {
        if (!IsSupported(metadata) || string.IsNullOrEmpty(password))
        {
            return new DecryptedSource(VaultImportStatus.Unavailable, Array.Empty<User>(), null);
        }

        var salt = Convert.FromBase64String(metadata.KdfSalt);
        var key = keyService.DeriveKey(password, salt, metadata.KdfIterations);

        if (!keyService.VerifyKey(key, metadata.Verifier))
        {
            return new DecryptedSource(VaultImportStatus.InvalidPassword, Array.Empty<User>(), key);
        }

        try
        {
            var decrypted = entries.Select(entry => new User
            {
                Id = entry.Id,
                Site = cipher.Decrypt(key, entry.Site),
                Login = cipher.Decrypt(key, entry.Login),
                Password = cipher.Decrypt(key, entry.Password),
                CreationDate = entry.CreationDate,
                LastUpdated = entry.LastUpdated
            }).ToArray();

            return new DecryptedSource(VaultImportStatus.Success, decrypted, key);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            return new DecryptedSource(VaultImportStatus.InvalidPassword, Array.Empty<User>(), key);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(key);
            throw;
        }
    }

    private DecryptedSource DecryptLegacySource(IReadOnlyList<User> entries, string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return new DecryptedSource(VaultImportStatus.InvalidPassword, Array.Empty<User>(), null);
        }

        var decrypted = new List<User>();

        foreach (var entry in entries)
        {
            var prepared = DecryptLegacyEntry(entry, password);

            if (prepared is null)
            {
                return new DecryptedSource(VaultImportStatus.InvalidPassword, Array.Empty<User>(), null);
            }

            decrypted.Add(prepared);
        }

        return new DecryptedSource(VaultImportStatus.Success, decrypted, null);
    }

    private async Task<MergePlan> BuildMergePlanAsync(IReadOnlyList<User> sourceEntries)
    {
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

        return new MergePlan(inserts, updates, unchanged, skipped, conflicts);
    }

    private async Task<VaultImportResult> ApplyMergePlanAsync(MergePlan plan)
    {
        if (!plan.HasChanges)
        {
            return CreateResult(plan, null);
        }

        var safetyCopy = await CreateSafetyCopyAsync();
        await mergeRepository.ApplyAsync(plan.Inserts, plan.Updates);

        return CreateResult(plan, safetyCopy);
    }

    private async Task<string> CreateSafetyCopyAsync()
    {
        var timestamp = timeProvider.GetLocalNow().ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var safetyCopy = $"{backupService.DatabasePath}.before-import-{timestamp}.bak";

        await backupService.BackupAsync(safetyCopy);

        return safetyCopy;
    }

    private static VaultImportResult CreateResult(MergePlan plan, string? safetyCopyPath) =>
        new(
            VaultImportStatus.Success,
            plan.Inserts.Count,
            plan.Updates.Count,
            plan.Unchanged,
            plan.Skipped,
            plan.Conflicts,
            safetyCopyPath);

    private static bool IsSupported(VaultMetadata metadata) =>
        metadata.FormatVersion == VaultMetadata.CurrentFormatVersion
        && string.Equals(metadata.Kdf, VaultMetadata.Pbkdf2Sha256, StringComparison.Ordinal)
        && metadata.KdfIterations is >= VaultMetadata.MinimumSupportedIterations and <= VaultMetadata.MaximumSupportedIterations
        && metadata.KdfSalt.Length > 0
        && metadata.Verifier.Length > 0;

    private User? DecryptLegacyEntry(User entry, string password)
    {
        try
        {
            return new User
            {
                Id = entry.Id,
                Site = legacyCipher.Decrypt(entry.Site, password),
                Login = legacyCipher.Decrypt(entry.Login, password),
                Password = legacyCipher.Decrypt(entry.Password, password),
                CreationDate = entry.CreationDate,
                LastUpdated = entry.LastUpdated
            };
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
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

    private sealed record MergePlan(
        IReadOnlyList<User> Inserts,
        IReadOnlyList<User> Updates,
        int Unchanged,
        int Skipped,
        int Conflicts)
    {
        public bool HasChanges => Inserts.Count > 0 || Updates.Count > 0;
    }

    private sealed class DecryptedSource : IDisposable
    {
        private byte[]? _key;

        public DecryptedSource(VaultImportStatus status, IReadOnlyList<User> entries, byte[]? key)
        {
            Status = status;
            Entries = entries;
            _key = key;
        }

        public VaultImportStatus Status { get; }

        public IReadOnlyList<User> Entries { get; }

        public void Dispose()
        {
            if (_key is null)
            {
                return;
            }

            CryptographicOperations.ZeroMemory(_key);
            _key = null;
        }
    }
}
