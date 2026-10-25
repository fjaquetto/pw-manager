using Microsoft.Extensions.Time.Testing;
using Moq;
using PWManager.Application.UseCases.Unlock;
using PWManager.Domain.Vault;
using PWManager.ViewModels;

namespace PWManager.UnitTests;

public class InteractionTests
{
    [Fact]
    public async Task Visibility_ResetsOnSelection_Edit_SaveAndCancel()
    {
        var vault = new TestVault(new User("A", "login", "pw"), new("B", "login", "pw"));
        var viewModel = vault.ViewModel;

        await viewModel.LoadEntriesAsync();
        var first = viewModel.Entries[0];
        viewModel.SelectedEntry = first;
        viewModel.ToggleDetailPasswordCommand.Execute(null);

        Assert.True(first.IsPasswordVisible);
        viewModel.SelectedEntry = viewModel.Entries[1];

        Assert.False(first.IsPasswordVisible);
        viewModel.EditEntryCommand.Execute(null);
        viewModel.Editor.IsPasswordVisible = true;

        await viewModel.SaveEntryCommand.ExecuteAsync(null);

        Assert.False(viewModel.Editor.IsPasswordVisible);
        Assert.False(viewModel.SelectedEntry!.IsPasswordVisible);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Clipboard_OnlyReportsSuccessWhenAvailable(bool available)
    {
        var vault = new TestVault();
        vault.Clipboard.Setup(c => c.SetTextAsync("secret")).ReturnsAsync(available);

        await vault.ViewModel.CopyTextAsync("secret", "Password");

        Assert.Equal(!available, vault.ViewModel.Notification.IsError);
        vault.Clipboard.Verify(c => c.SetTextAsync("secret"), Times.Once);
    }

    [Fact]
    public async Task ClipboardException_IsPresentedAsAnError()
    {
        var vault = new TestVault();
        vault.Clipboard.Setup(c => c.SetTextAsync(It.IsAny<string>())).ThrowsAsync(new IOException());

        await vault.ViewModel.CopyTextAsync("secret");

        Assert.True(vault.ViewModel.Notification.IsError);
    }

    [Fact]
    public void Generator_EditorUseChangesOnlyDraft_AndRestoresMask()
    {
        var vault = new TestVault();
        vault.Fill();
        var viewModel = vault.ViewModel;
        viewModel.Editor.IsPasswordVisible = true;
        viewModel.OpenEditorGeneratorCommand.Execute(null);

        Assert.False(viewModel.Editor.IsPasswordVisible);
        viewModel.Generator.IsPasswordVisible = true;
        var generated = viewModel.Generator.GeneratedPassword;
        viewModel.UseGeneratedPasswordCommand.Execute(null);

        Assert.Equal(generated, viewModel.Editor.Password);
        Assert.False(viewModel.IsGeneratorOpen);
        Assert.False(viewModel.Generator.IsPasswordVisible);
        Assert.Empty(vault.Users);
    }

    [Fact]
    public void Generator_FromHeader_CreatesDraftAndPreservesOptions()
    {
        var viewModel = new TestVault().ViewModel;
        viewModel.OpenGeneratorCommand.Execute(null);
        viewModel.Generator.Length = 48;
        viewModel.Generator.UseSymbols = false;
        var password = viewModel.Generator.GeneratedPassword;
        viewModel.UseGeneratedPasswordCommand.Execute(null);

        Assert.True(viewModel.IsEditorOpen);
        Assert.Equal(password, viewModel.Editor.Password);
        Assert.False(viewModel.Generator.UseSymbols);
        Assert.Equal(48, viewModel.Generator.Length);
    }

    [Fact]
    public void Generator_FromHeaderDuringDirtyEdit_RequestsDiscardBeforeReplacing()
    {
        var vault = new TestVault();
        vault.Fill();
        var viewModel = vault.ViewModel;
        viewModel.OpenGeneratorCommand.Execute(null);
        viewModel.UseGeneratedPasswordCommand.Execute(null);

        Assert.True(viewModel.IsDiscardPending);
        Assert.Equal("Personal account", viewModel.Editor.Site);
        viewModel.KeepEditingCommand.Execute(null);

        Assert.Equal("Personal account", viewModel.Editor.Site);
    }

    [Theory]
    [InlineData(1, 8)]
    [InlineData(49, 48)]
    [InlineData(20, 20)]
    public void Generator_LengthClampsAndRegenerates(int requested, int expected)
    {
        var viewModel = new PasswordGeneratorViewModel
        {
            Length = requested
        };

        Assert.Equal(expected, viewModel.Length);
        Assert.Equal(expected, viewModel.GeneratedPassword.Length);
    }

    [Fact]
    public void Generator_FallbackIsExplicitAndUsesLowercase()
    {
        var viewModel = new PasswordGeneratorViewModel
        {
            UseUppercase = false,
            UseLowercase = false,
            UseNumbers = false,
            UseSymbols = false
        };

        Assert.True(viewModel.UsesFallback);
        Assert.All(viewModel.GeneratedPassword, c => Assert.True(char.IsLower(c)));
    }

    [Fact]
    public async Task Notifications_ExpireOnFakeTime_AndOldTimerCannotHideNewMessage()
    {
        var time = new FakeTimeProvider();
        using var viewModel = new NotificationViewModel(time);
        viewModel.Show("First");
        time.Advance(TimeSpan.FromSeconds(2));
        viewModel.Show("Second");
        time.Advance(TimeSpan.FromSeconds(1));

        Assert.True(viewModel.IsVisible);
        Assert.Equal("Second", viewModel.Message);
        var hidden = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(viewModel.IsVisible) && !viewModel.IsVisible)
            {
                hidden.TrySetResult();
            }
        };
        time.Advance(TimeSpan.FromSeconds(2));

        await hidden.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(viewModel.IsVisible);
        viewModel.Show("Failure", isError: true);
        time.Advance(TimeSpan.FromDays(1));

        Assert.True(viewModel.IsVisible);
        viewModel.DismissCommand.Execute(null);

        Assert.False(viewModel.IsVisible);
    }

    [Theory]
    [InlineData(UnlockResult.InvalidPassword, false)]
    [InlineData(UnlockResult.Unavailable, false)]
    [InlineData(UnlockResult.Success, true)]
    public async Task Unlock_MapsResult_ClearsSensitiveFields_AndRaisesUnlockedOnlyOnSuccess(UnlockResult result, bool navigates)
    {
        var service = new Mock<IUnlockService>();
        service.Setup(s => s.UnlockAsync("master")).ReturnsAsync(result);
        var viewModel = new UnlockViewModel(service.Object, TestAppInfo.Create())
        {
            Password = "master",
            IsPasswordVisible = true
        };
        viewModel.Configure(VaultStatus.Existing);
        var unlocked = 0;
        viewModel.Unlocked += () => unlocked++;

        await viewModel.UnlockCommand.ExecuteAsync(null);

        Assert.Equal(!navigates, viewModel.HasError);
        Assert.Empty(viewModel.Password);
        Assert.False(viewModel.IsPasswordVisible);
        Assert.False(viewModel.IsBusy);
        Assert.Equal(navigates ? 1 : 0, unlocked);
    }

    [Fact]
    public async Task Unlock_EmptyInputDoesNotCallService()
    {
        var service = new Mock<IUnlockService>();
        var viewModel = new UnlockViewModel(service.Object, TestAppInfo.Create());
        viewModel.Configure(VaultStatus.Existing);

        await viewModel.UnlockCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        service.Verify(s => s.UnlockAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Unlock_WhilePending_DisablesCommand()
    {
        var source = new TaskCompletionSource<UnlockResult>();
        var service = new Mock<IUnlockService>();
        service.Setup(s => s.UnlockAsync(It.IsAny<string>())).Returns(source.Task);
        var viewModel = new UnlockViewModel(service.Object, TestAppInfo.Create())
        {
            Password = "master"
        };
        viewModel.Configure(VaultStatus.Existing);
        var task = viewModel.UnlockCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsBusy);
        Assert.True(viewModel.ShowProgress);
        Assert.False(viewModel.UnlockCommand.CanExecute(null));
        Assert.Equal("Unlocking…", viewModel.ButtonLabel);
        source.SetResult(UnlockResult.Success);

        await task;

        Assert.True(viewModel.IsIdle);
    }
}
