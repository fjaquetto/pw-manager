using Microsoft.Extensions.Time.Testing;
using Moq;
using PWManager.Application.UseCases.Settings;
using PWManager.Application.UseCases.Unlock;
using PWManager.Domain.Abstractions.Infrastructure;
using PWManager.Domain.Vault;
using PWManager.Infra.Configuration;
using PWManager.Infra.Security;
using PWManager.ViewModels;

namespace PWManager.UnitTests;

public class ShellViewModelTests
{
    [Fact]
    public void StartsLockedWithoutCreatingTheVault()
    {
        var created = 0;
        var shell = CreateShell(() => { created++; return new TestVault().ViewModel; });

        Assert.True(shell.ShowUnlock);
        Assert.False(shell.IsUnlocked);
        Assert.False(shell.IsOpening);
        Assert.False(shell.ShowProgress);
        Assert.Null(shell.Vault);
        Assert.Equal(0, created);
    }

    [Fact]
    public async Task SuccessfulUnlock_LoadsTheVaultBeforeRevealingIt()
    {
        var encrypted = new TaskCompletionSource<IReadOnlyList<User>>();
        var vault = new TestVault();
        vault.UserRepository.Setup(repository => repository.GetAllAsync()).Returns(encrypted.Task);
        var shell = CreateShell(() => vault.ViewModel, UnlockResult.Success);

        await shell.Unlock.UnlockCommand.ExecuteAsync(null);

        Assert.True(shell.IsOpening);
        Assert.True(shell.ShowProgress);
        Assert.False(shell.IsUnlocked);
        Assert.True(shell.ShowUnlock);

        encrypted.SetResult(Array.Empty<User>());

        await shell.OpeningTask!;

        Assert.False(shell.IsOpening);
        Assert.True(shell.IsUnlocked);
        Assert.False(shell.ShowUnlock);
        Assert.NotNull(shell.Vault);
    }

    [Fact]
    public async Task SuccessfulUnlock_CreatesTheVaultExactlyOnce()
    {
        var created = 0;
        var shell = CreateShell(() => { created++; return new TestVault().ViewModel; }, UnlockResult.Success);

        await shell.Unlock.UnlockCommand.ExecuteAsync(null);
        await shell.OpeningTask!;
        await shell.OpenVaultAsync();

        Assert.Equal(1, created);
    }

    [Theory]
    [InlineData(UnlockResult.InvalidPassword)]
    [InlineData(UnlockResult.Unavailable)]
    public async Task FailedUnlock_NeverCreatesTheVault(UnlockResult result)
    {
        var created = 0;
        var shell = CreateShell(() => { created++; return new TestVault().ViewModel; }, result);

        await shell.Unlock.UnlockCommand.ExecuteAsync(null);

        Assert.True(shell.ShowUnlock);
        Assert.False(shell.IsUnlocked);
        Assert.Null(shell.Vault);
        Assert.Null(shell.OpeningTask);
        Assert.Equal(0, created);
        Assert.True(shell.Unlock.HasError);
    }

    [Fact]
    public async Task ProgressIndicator_CoversPasswordValidationAndVaultLoading()
    {
        var pendingUnlock = new TaskCompletionSource<UnlockResult>();
        var unlockService = new Mock<IUnlockService>();
        unlockService.Setup(s => s.UnlockAsync(It.IsAny<string>())).Returns(pendingUnlock.Task);
        var encrypted = new TaskCompletionSource<IReadOnlyList<User>>();
        var vault = new TestVault();
        vault.UserRepository.Setup(repository => repository.GetAllAsync()).Returns(encrypted.Task);
        var shell = CreateShell(() => vault.ViewModel, unlockService: unlockService.Object);

        shell.Unlock.Password = "master";
        var unlocking = shell.Unlock.UnlockCommand.ExecuteAsync(null);

        Assert.True(shell.ShowProgress);

        pendingUnlock.SetResult(UnlockResult.Success);

        await unlocking;

        Assert.True(shell.ShowProgress);
        Assert.True(shell.IsOpening);
        Assert.False(shell.IsUnlocked);

        encrypted.SetResult(Array.Empty<User>());
        await shell.OpeningTask!;

        Assert.False(shell.ShowProgress);
        Assert.True(shell.IsUnlocked);
    }

    [Fact]
    public async Task Lock_DisposesTheVaultZeroesTheSessionAndReturnsToTheUnlockScreen()
    {
        var session = new VaultSession();
        var shell = await UnlockShellAsync(session);
        var vault = shell.Vault;

        shell.Unlock.Password = "typed";
        shell.RequestLock();

        Assert.False(shell.IsUnlocked);
        Assert.True(shell.ShowUnlock);
        Assert.Null(shell.Vault);
        Assert.False(session.IsUnlocked);
        Assert.Empty(shell.Unlock.Password);
        Assert.Null(vault!.SelectedEntry);
    }

    [Fact]
    public async Task Lock_WhileTheVaultIsBusy_PostponesUntilIdle()
    {
        var time = new FakeTimeProvider();
        var session = new VaultSession();
        var shell = await UnlockShellAsync(session, time);
        shell.Vault!.IsBusy = true;

        shell.RequestLock();

        Assert.True(shell.IsUnlocked);

        shell.Vault.IsBusy = false;
        time.Advance(TimeSpan.FromSeconds(1));

        Assert.False(shell.IsUnlocked);
        Assert.False(session.IsUnlocked);
    }

    [Fact]
    public async Task AutoLock_LocksAfterTheConfiguredIdleTime()
    {
        var time = new FakeTimeProvider();
        var settings = CreateSettings();
        settings.Update(settings.Current with { AutoLockMinutes = 1 });
        var session = new VaultSession();
        var shell = await UnlockShellAsync(session, time, settings);

        time.Advance(TimeSpan.FromSeconds(30));

        Assert.True(shell.IsUnlocked);

        time.Advance(TimeSpan.FromSeconds(31));

        Assert.False(shell.IsUnlocked);
        Assert.False(session.IsUnlocked);
    }

    [Fact]
    public async Task Activity_PostponesTheAutoLock()
    {
        var time = new FakeTimeProvider();
        var settings = CreateSettings();
        settings.Update(settings.Current with { AutoLockMinutes = 1 });
        var shell = await UnlockShellAsync(new VaultSession(), time, settings);

        time.Advance(TimeSpan.FromSeconds(45));
        shell.NotifyActivity();
        time.Advance(TimeSpan.FromSeconds(45));

        Assert.True(shell.IsUnlocked);

        time.Advance(TimeSpan.FromSeconds(20));

        Assert.False(shell.IsUnlocked);
    }

    [Fact]
    public async Task AutoLock_IsDisabledWhenTheTimeoutIsZero()
    {
        var time = new FakeTimeProvider();
        var settings = CreateSettings();
        settings.Update(settings.Current with { AutoLockMinutes = 0 });
        var shell = await UnlockShellAsync(new VaultSession(), time, settings);

        time.Advance(TimeSpan.FromHours(5));

        Assert.True(shell.IsUnlocked);
    }

    [Fact]
    public async Task MinimizeLock_OnlyWhenTheOptionIsEnabled()
    {
        var settings = CreateSettings();
        var shell = await UnlockShellAsync(new VaultSession(), settings: settings);

        shell.HandleWindowMinimized();

        Assert.True(shell.IsUnlocked);

        settings.Update(settings.Current with { LockWhenMinimized = true });
        shell.HandleWindowMinimized();

        Assert.False(shell.IsUnlocked);
    }

    [Fact]
    public async Task RestoringABackup_LocksTheVaultAndShowsTheNotice()
    {
        var vault = new TestVault();
        vault.FilePicker.Setup(p => p.PickOpenFileAsync(It.IsAny<string>())).ReturnsAsync("/tmp/backup.db");
        vault.Backup.Setup(b => b.RestoreAsync("/tmp/backup.db")).ReturnsAsync("/tmp/safety.bak");
        var session = new VaultSession();
        var shell = await UnlockShellAsync(session, vaultFactory: () => vault.ViewModel);

        await vault.ViewModel.SecuritySettings.ChooseRestoreCommand.ExecuteAsync(null);
        await vault.ViewModel.SecuritySettings.ConfirmRestoreCommand.ExecuteAsync(null);

        Assert.False(shell.IsUnlocked);
        Assert.False(session.IsUnlocked);
        Assert.Contains("Backup restored", shell.Unlock.Notice);
    }

    [Fact]
    public async Task VaultLockCommand_LocksTheVault()
    {
        var session = new VaultSession();
        var shell = await UnlockShellAsync(session);

        shell.Vault!.RequestLockCommand.Execute(null);

        Assert.False(shell.IsUnlocked);
        Assert.False(session.IsUnlocked);
        Assert.Empty(shell.Unlock.Notice);
    }

    private static AppSettingsService CreateSettings() =>
        new(new AppSettingsStore(Path.Combine(Path.GetTempPath(), "pwmanager-shell-tests-" + Guid.NewGuid().ToString("N"))));

    private static ShellViewModel CreateShell(
        Func<MainViewModel> factory,
        UnlockResult result = UnlockResult.Success,
        IUnlockService? unlockService = null,
        AppSettingsService? settings = null,
        FakeTimeProvider? time = null,
        IVaultSession? session = null)
    {
        if (unlockService is null)
        {
            var mock = new Mock<IUnlockService>();
            mock.Setup(s => s.UnlockAsync(It.IsAny<string>())).ReturnsAsync(result);
            unlockService = mock.Object;
        }

        var unlock = new UnlockViewModel(unlockService, TestAppInfo.Create())
        {
            Password = "master"
        };
        unlock.Configure(VaultStatus.Existing);

        return new ShellViewModel(
            unlock,
            factory,
            session ?? new VaultSession(),
            settings ?? CreateSettings(),
            TestAppInfo.Create(),
            time ?? new FakeTimeProvider());
    }

    private static async Task<ShellViewModel> UnlockShellAsync(
        IVaultSession session,
        FakeTimeProvider? time = null,
        AppSettingsService? settings = null,
        Func<MainViewModel>? vaultFactory = null)
    {
        var shell = CreateShell(vaultFactory ?? (() => new TestVault().ViewModel), UnlockResult.Success, settings: settings, time: time, session: session);

        await shell.Unlock.UnlockCommand.ExecuteAsync(null);
        await shell.OpeningTask!;

        session.Unlock(new byte[32]);
        Assert.True(shell.IsUnlocked);

        return shell;
    }
}
