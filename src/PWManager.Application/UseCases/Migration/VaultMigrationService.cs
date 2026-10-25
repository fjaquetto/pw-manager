using System.Security.Cryptography;
using PWManager.Domain.Abstractions.Infrastructure;
using PWManager.Domain.Abstractions.Repositories;
using PWManager.Domain.Vault;

namespace PWManager.Application.UseCases.Migration;

public sealed class VaultMigrationService(
    IVaultMigrationRepository repository,
    IVaultBackupService backupService,
    IVaultCipher cipher,
    ILegacyVaultCipher legacyCipher) : IVaultMigrationService
{
    public async Task<VaultMigrationResult> MigrateLegacyAsync(
        string legacyPassword,
        byte[] newKey,
        VaultMetadata newMetadata)
    {
        var backupPath = $"{backupService.DatabasePath}.migrating.bak";

        try
        {
            await backupService.BackupAsync(backupPath);

            var state = await repository.LoadAsync();

            if (state.Metadata is not null)
            {
                DeleteBackup(backupPath);
                return VaultMigrationResult.Failed;
            }

            foreach (var entry in state.Entries)
            {
                entry.Site = cipher.Encrypt(newKey, legacyCipher.Decrypt(entry.Site, legacyPassword));
                entry.Login = cipher.Encrypt(newKey, legacyCipher.Decrypt(entry.Login, legacyPassword));
                entry.Password = cipher.Encrypt(newKey, legacyCipher.Decrypt(entry.Password, legacyPassword));
            }

            VerifyEntries(state.Entries, newKey);
            await repository.SaveAsync(state.Entries, newMetadata);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            DeleteBackup(backupPath);
            return VaultMigrationResult.InvalidPassword;
        }
        catch (Exception)
        {
            return VaultMigrationResult.Failed;
        }

        DeleteBackup(backupPath);
        return VaultMigrationResult.Success;
    }

    public async Task<VaultMigrationResult> ReencryptAsync(
        byte[] currentKey,
        byte[] newKey,
        VaultMetadata updatedMetadata)
    {
        var backupPath = $"{backupService.DatabasePath}.rekey.bak";

        try
        {
            await backupService.BackupAsync(backupPath);

            var state = await repository.LoadAsync();

            if (state.Metadata is null)
            {
                DeleteBackup(backupPath);
                return VaultMigrationResult.Failed;
            }

            foreach (var entry in state.Entries)
            {
                entry.Site = cipher.Encrypt(newKey, cipher.Decrypt(currentKey, entry.Site));
                entry.Login = cipher.Encrypt(newKey, cipher.Decrypt(currentKey, entry.Login));
                entry.Password = cipher.Encrypt(newKey, cipher.Decrypt(currentKey, entry.Password));
            }

            VerifyEntries(state.Entries, newKey);
            await repository.SaveAsync(state.Entries, updatedMetadata);
        }
        catch (Exception)
        {
            return VaultMigrationResult.Failed;
        }

        DeleteBackup(backupPath);
        return VaultMigrationResult.Success;
    }

    private void VerifyEntries(IReadOnlyList<User> entries, byte[] key)
    {
        foreach (var entry in entries)
        {
            cipher.Decrypt(key, entry.Site);
            cipher.Decrypt(key, entry.Login);
            cipher.Decrypt(key, entry.Password);
        }
    }

    private static void DeleteBackup(string backupPath)
    {
        try
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
