using Microsoft.Extensions.Time.Testing;
using Moq;
using PWManager.Application.UseCases.Settings;
using PWManager.Application.UseCases.VaultEntries;
using PWManager.Application.UseCases.Maintenance;
using PWManager.Application.UseCases.Rekey;
using PWManager.Domain.Abstractions.Infrastructure;
using PWManager.Domain.Abstractions.Repositories;
using PWManager.Domain.Vault;
using PWManager.Infra.Configuration;
using PWManager.Services.Interfaces;
using PWManager.ViewModels;

namespace PWManager.UnitTests;

internal sealed class TestVault
{
    public Mock<IUserRepository> UserRepository { get; } = new();

    public Mock<IUserEncryptorService> Encryptor { get; } = new();

    public Mock<IClipboardService> Clipboard { get; } = new();

    public Mock<IVaultRekeyService> Rekey { get; } = new();

    public Mock<IVaultMaintenanceService> Backup { get; } = new();

    public Mock<IFilePickerService> FilePicker { get; } = new();

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero));

    public AppSettingsService Settings { get; }

    public List<User> Users { get; } = new();

    public MainViewModel ViewModel { get; }

    public TestVault(params User[] users)
    {
        Users.AddRange(users);
        UserRepository.Setup(repository => repository.GetAllAsync())
            .ReturnsAsync(() => (IReadOnlyList<User>)Users.ToArray());
        UserRepository.Setup(repository => repository.GetUserByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => Users.FirstOrDefault(user => user.Id == id));
        UserRepository.Setup(repository => repository.AddAsync(It.IsAny<User>())).Callback<User>(Users.Add).Returns(Task.CompletedTask);
        UserRepository.Setup(repository => repository.UpdateAsync(It.IsAny<User>())).Callback<User>(user =>
        {
            Users.RemoveAll(existing => existing.Id == user.Id);
            Users.Add(user);
        }).Returns(Task.CompletedTask);
        UserRepository.Setup(repository => repository.DeleteAsync(It.IsAny<User>())).Callback<User>(user => Users.RemoveAll(existing => existing.Id == user.Id)).Returns(Task.CompletedTask);
        Encryptor.Setup(encryptor => encryptor.EncryptUser(It.IsAny<User>())).Returns((User user) => user);
        Encryptor.Setup(encryptor => encryptor.DecryptUser(It.IsAny<User>())).Returns((User user) => user);
        Clipboard.Setup(clipboard => clipboard.SetTextAsync(It.IsAny<string>())).ReturnsAsync(true);
        Settings = new AppSettingsService(new AppSettingsStore(Path.Combine(Path.GetTempPath(), "pwmanager-tests-settings-" + Guid.NewGuid().ToString("N"))));

        var themeSettings = new ThemeSettingsViewModel(Mock.Of<IThemeService>(), new ColorThemeStore(UnusedThemeDirectory));
        var securitySettings = new SecuritySettingsViewModel(Settings, Rekey.Object, Backup.Object, FilePicker.Object);

        ViewModel = new(
            new VaultEntryService(UserRepository.Object, Encryptor.Object, Time),
            Clipboard.Object,
            Time,
            Settings,
            themeSettings,
            securitySettings,
            TestAppInfo.Create());
    }

    private static string UnusedThemeDirectory => Path.Combine(Path.GetTempPath(), "pwmanager-tests-no-themes");

    public void Fill(string site = "Personal account", string login = "name@example.com", string password = "  exact secret  ")
    {
        ViewModel.NewEntryCommand.Execute(null);
        ViewModel.Editor.Site = site;
        ViewModel.Editor.Login = login;
        ViewModel.Editor.Password = password;
    }
}
