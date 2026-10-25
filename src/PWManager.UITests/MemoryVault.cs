using PWManager.Application.UseCases.VaultEntries;
using PWManager.Domain.Abstractions.Infrastructure;
using PWManager.Domain.Abstractions.Repositories;
using PWManager.Domain.Vault;
using PWManager.Infra.Configuration;
using PWManager.Services;
using PWManager.Services.Interfaces;
using PWManager.UITests.Fakes;
using PWManager.ViewModels;

namespace PWManager.UITests;

internal sealed class MemoryVault : IUserRepository, IUserEncryptorService, IClipboardService
{
    public List<User> Users { get; } = new();

    public string? CopiedText { get; private set; }

    public Task<IReadOnlyList<User>> GetAllAsync() => Task.FromResult<IReadOnlyList<User>>(Users.ToArray());

    public Task<bool> AnyAsync() => Task.FromResult(Users.Count > 0);

    public Task<User?> GetUserByIdAsync(Guid id) => Task.FromResult(Users.FirstOrDefault(user => user.Id == id));

    public Task AddAsync(User user)
    {
        Users.Add(user);

        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user)
    {
        Users.RemoveAll(existing => existing.Id == user.Id);
        Users.Add(user);

        return Task.CompletedTask;
    }

    public Task DeleteAsync(User user)
    {
        Users.RemoveAll(existing => existing.Id == user.Id);

        return Task.CompletedTask;
    }

    public User EncryptUser(User user) => user;

    public User DecryptUser(User user) => user;

    public Task<bool> SetTextAsync(string text)
    {
        CopiedText = text;

        return Task.FromResult(true);
    }

    public Task<string?> TryGetTextAsync() => Task.FromResult<string?>(CopiedText);

    public Task ClearAsync()
    {
        CopiedText = null;

        return Task.CompletedTask;
    }

    public AppSettingsFake Settings { get; } = new();

    public RekeyStub Rekey { get; } = new();

    public BackupStub Backup { get; } = new();

    public FilePickerStub FilePicker { get; } = new();

    public MainViewModel CreateViewModel()
    {
        var themeSettings = new ThemeSettingsViewModel(new ThemeService(), new ColorThemeStore(ThemeDirectory));
        var securitySettings = new SecuritySettingsViewModel(Settings, Rekey, Backup, FilePicker);

        return new MainViewModel(
            new VaultEntryService(this, this, TimeProvider.System),
            this,
            TimeProvider.System,
            Settings,
            themeSettings,
            securitySettings,
            new AppInfo("2.3.1", "3481920"));
    }

    private static string ThemeDirectory => Path.Combine(Path.GetTempPath(), "pwmanager-ui-themes-unused");

    public static MemoryVault Sample(int count = 7)
    {
        var vault = new MemoryVault();
        var names = new[]
        {
            "Figma",
            "GitHub",
            "Google",
            "Linear",
            "Notion",
            "Spotify",
            "Work account"
        };
        for (var i = 0; i < count; i++)
        {
            var site = names[i % names.Length] + (i >= names.Length ? $" {i:D4}" : "");
            var login = i % 2 == 0 ? "alex@example.com" : "alex.morgan";

            vault.Users.Add(new(site, login, "Example-password-42!")
            {
                CreationDate = new(2026, 2, 18, 9, 30, 0),
                LastUpdated = new(2026, 8, 28, 14, 15, 0)
            });
        }

        return vault;
    }
}
