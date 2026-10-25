using Moq;
using PWManager.Domain.Vault;

namespace PWManager.UnitTests;

public class ClipboardHygieneTests
{
    [Fact]
    public async Task SensitiveCopy_SchedulesAClearAndAnnouncesIt()
    {
        var vault = new TestVault(new User("Site", "login", "  exact secret  "));
        vault.Clipboard.Setup(c => c.TryGetTextAsync()).ReturnsAsync("  exact secret  ");
        await vault.ViewModel.LoadEntriesAsync();
        var entry = vault.ViewModel.Entries[0];

        await vault.ViewModel.CopyPasswordCommand.ExecuteAsync(entry);

        Assert.Contains("clears in 25s", vault.ViewModel.Notification.Message);

        vault.Time.Advance(TimeSpan.FromSeconds(25));

        vault.Clipboard.Verify(c => c.ClearAsync(), Times.Once);
        Assert.Equal("Clipboard cleared.", vault.ViewModel.Notification.Message);
    }

    [Fact]
    public async Task NonSensitiveCopy_IsNotCleared()
    {
        var vault = new TestVault(new User("Site", "login", "  exact secret  "));
        vault.Clipboard.Setup(c => c.TryGetTextAsync()).ReturnsAsync("name@example.com");
        await vault.ViewModel.LoadEntriesAsync();
        vault.ViewModel.SelectedEntry = vault.ViewModel.Entries[0];

        await vault.ViewModel.CopyLoginCommand.ExecuteAsync(null);

        Assert.Equal("Login copied.", vault.ViewModel.Notification.Message);

        vault.Time.Advance(TimeSpan.FromMinutes(5));

        vault.Clipboard.Verify(c => c.ClearAsync(), Times.Never);
    }

    [Fact]
    public async Task ClipboardClear_DoesNotTouchContentCopiedElsewhere()
    {
        var vault = new TestVault(new User("Site", "login", "  exact secret  "));
        vault.Clipboard.Setup(c => c.TryGetTextAsync()).ReturnsAsync("something else");
        await vault.ViewModel.LoadEntriesAsync();

        await vault.ViewModel.CopyPasswordCommand.ExecuteAsync(vault.ViewModel.Entries[0]);
        vault.Time.Advance(TimeSpan.FromSeconds(25));

        vault.Clipboard.Verify(c => c.ClearAsync(), Times.Never);
    }

    [Fact]
    public async Task ClipboardClear_IsDisabledWhenConfiguredToZero()
    {
        var vault = new TestVault(new User("Site", "login", "  exact secret  "));
        vault.Settings.Update(vault.Settings.Current with { ClipboardClearSeconds = 0 });
        vault.Clipboard.Setup(c => c.TryGetTextAsync()).ReturnsAsync("  exact secret  ");
        await vault.ViewModel.LoadEntriesAsync();

        await vault.ViewModel.CopyPasswordCommand.ExecuteAsync(vault.ViewModel.Entries[0]);

        Assert.Equal("Password copied.", vault.ViewModel.Notification.Message);

        vault.Time.Advance(TimeSpan.FromMinutes(10));

        vault.Clipboard.Verify(c => c.ClearAsync(), Times.Never);
    }

    [Fact]
    public async Task LockingTheVault_ClearsTheSensitiveClipboardImmediately()
    {
        var vault = new TestVault(new User("Site", "login", "  exact secret  "));
        vault.Clipboard.Setup(c => c.TryGetTextAsync()).ReturnsAsync("  exact secret  ");
        await vault.ViewModel.LoadEntriesAsync();

        await vault.ViewModel.CopyPasswordCommand.ExecuteAsync(vault.ViewModel.Entries[0]);

        vault.ViewModel.Dispose();

        vault.Clipboard.Verify(c => c.ClearAsync(), Times.Once);
    }

    [Fact]
    public async Task RevealedPassword_IsHiddenAfterTheConfiguredTimeout()
    {
        var vault = new TestVault(new User("Site", "login", "  exact secret  "));
        await vault.ViewModel.LoadEntriesAsync();
        var entry = vault.ViewModel.Entries[0];
        vault.ViewModel.SelectedEntry = entry;

        vault.ViewModel.ToggleDetailPasswordCommand.Execute(null);

        Assert.True(entry.IsPasswordVisible);

        vault.Time.Advance(TimeSpan.FromSeconds(20));

        Assert.False(entry.IsPasswordVisible);
    }

    [Fact]
    public async Task HidingSensitiveData_MasksImmediatelyAndCancelsTheTimer()
    {
        var vault = new TestVault(new User("Site", "login", "  exact secret  "));
        await vault.ViewModel.LoadEntriesAsync();
        var entry = vault.ViewModel.Entries[0];
        vault.ViewModel.SelectedEntry = entry;
        vault.ViewModel.ToggleDetailPasswordCommand.Execute(null);

        vault.ViewModel.HideSensitiveData();

        Assert.False(entry.IsPasswordVisible);

        vault.Time.Advance(TimeSpan.FromMinutes(5));

        Assert.False(entry.IsPasswordVisible);
    }

    [Fact]
    public async Task AutoHide_IsDisabledWhenConfiguredToZero()
    {
        var vault = new TestVault(new User("Site", "login", "  exact secret  "));
        vault.Settings.Update(vault.Settings.Current with { HidePasswordsAfterSeconds = 0 });
        await vault.ViewModel.LoadEntriesAsync();
        var entry = vault.ViewModel.Entries[0];
        vault.ViewModel.SelectedEntry = entry;
        vault.ViewModel.ToggleDetailPasswordCommand.Execute(null);

        vault.Time.Advance(TimeSpan.FromMinutes(10));

        Assert.True(entry.IsPasswordVisible);
    }
}
