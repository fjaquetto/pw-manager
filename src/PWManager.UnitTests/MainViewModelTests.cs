using Moq;
using PWManager.Domain.Vault;
using PWManager.ViewModels;

namespace PWManager.UnitTests;

public class MainViewModelTests
{
    [Fact]
    public async Task Search_CombinesGlobalOrWithSpecificAnd_IgnoresCase()
    {
        var vault = new TestVault(new User("GitHub", "alice", "a"), new("GitHub", "bob", "b"), new("Other", "github-alice", "c"));
        var viewModel = vault.ViewModel;

        await viewModel.LoadEntriesAsync();

        Assert.Null(viewModel.SelectedEntry);
        viewModel.SearchGlobal = "GITHUB";

        Assert.Equal(3, viewModel.Entries.Count);
        viewModel.FilterSite = "git";
        viewModel.FilterLogin = "ALICE";

        Assert.Single(viewModel.Entries);
        Assert.Equal("alice", viewModel.Entries[0].Login);
        Assert.Equal("1 of 3 entries", viewModel.CountLabel);
        Assert.Equal(3, viewModel.FilterCount);
        viewModel.ClearFiltersCommand.Execute(null);

        Assert.Equal(3, viewModel.Entries.Count);
        Assert.False(viewModel.HasFilters);
    }

    [Theory]
    [InlineData(0, "Alpha")]
    [InlineData(1, "Zulu")]
    [InlineData(2, "Zulu")]
    [InlineData(3, "Alpha")]
    [InlineData(4, "Zulu")]
    [InlineData(5, "Alpha")]
    [InlineData(6, "Alpha")]
    [InlineData(7, "Zulu")]
    public async Task Sort_AllDirections(int sort, string expected)
    {
        var a = new User("Alpha", "z-login", "a")
        {
            CreationDate = new(2020, 1, 1),
            LastUpdated = new(2026, 1, 1)
        };
        var z = new User("Zulu", "a-login", "b")
        {
            CreationDate = new(2025, 1, 1),
            LastUpdated = new(2021, 1, 1)
        };
        var viewModel = new TestVault(z, a).ViewModel;

        await viewModel.LoadEntriesAsync();
        viewModel.SortIndex = sort;

        Assert.Equal(expected, viewModel.Entries[0].Site);
    }

    [Fact]
    public async Task EmptyAndNoResults_AreDifferentStates()
    {
        var empty = new TestVault().ViewModel;

        await empty.LoadEntriesAsync();

        Assert.True(empty.IsVaultEmpty);
        Assert.False(empty.NoResults);
        var full = new TestVault(new User("Site", "name", "password")).ViewModel;

        await full.LoadEntriesAsync();
        full.SearchGlobal = "missing";

        Assert.True(full.NoResults);
        Assert.False(full.IsVaultEmpty);
    }

    [Fact]
    public async Task Create_PreservesFreeformSiteAndExactPassword_UsesEncryption()
    {
        var vault = new TestVault();
        vault.Fill();

        await vault.ViewModel.SaveEntryCommand.ExecuteAsync(null);
        var saved = Assert.Single(vault.Users);

        Assert.Equal("Personal account", saved.Site);
        Assert.Equal("  exact secret  ", saved.Password);
        vault.Encryptor.Verify(e => e.EncryptUser(It.IsAny<User>()), Times.Once);

        Assert.Equal(saved.Id, vault.ViewModel.SelectedEntry!.Id);
        Assert.False(vault.ViewModel.IsEditorOpen);
        Assert.False(vault.ViewModel.Notification.IsError);
    }

    [Fact]
    public async Task InvalidEditor_DoesNotPersist_ReportsFieldErrors()
    {
        var vault = new TestVault();
        vault.ViewModel.NewEntryCommand.Execute(null);

        await vault.ViewModel.SaveEntryCommand.ExecuteAsync(null);

        Assert.NotEmpty(vault.ViewModel.Editor.SiteError);
        Assert.NotEmpty(vault.ViewModel.Editor.LoginError);
        Assert.NotEmpty(vault.ViewModel.Editor.PasswordError);
        vault.UserRepository.Verify(repository => repository.AddAsync(It.IsAny<User>()), Times.Never);
        vault.ViewModel.Editor.Site = "Fixed";

        Assert.Empty(vault.ViewModel.Editor.SiteError);
    }

    [Fact]
    public async Task Edit_UsesDraft_PreservesIdAndCreation_UpdatesTimestamp()
    {
        var original = new User("Site", "old-login", "secret")
        {
            CreationDate = new(2020, 1, 1)
        };
        var vault = new TestVault(original);
        var viewModel = vault.ViewModel;

        await viewModel.LoadEntriesAsync();
        viewModel.SelectedEntry = viewModel.Entries[0];
        viewModel.EditEntryCommand.Execute(null);
        viewModel.Editor.Login = "new-login";

        Assert.Equal("old-login", viewModel.SelectedEntry.Login);
        Assert.Equal("old-login", original.Login);
        await viewModel.SaveEntryCommand.ExecuteAsync(null);
        var saved = Assert.Single(vault.Users);

        Assert.Equal(original.Id, saved.Id);
        Assert.Equal(original.CreationDate, saved.CreationDate);
        Assert.Equal(vault.Time.GetLocalNow().DateTime, saved.LastUpdated);
        Assert.Equal("new-login", viewModel.SelectedEntry!.Login);
    }

    [Fact]
    public async Task FilterHidesSavedEntry_SelectionAndClearActionRemainAvailable()
    {
        var vault = new TestVault(new User("Visible", "login", "pw"));
        var viewModel = vault.ViewModel;

        await viewModel.LoadEntriesAsync();
        viewModel.SearchGlobal = "visible";
        vault.Fill("Hidden");

        await viewModel.SaveEntryCommand.ExecuteAsync(null);

        Assert.True(viewModel.SavedEntryHidden);
        Assert.Equal("Hidden", viewModel.SelectedEntry!.Site);
        Assert.Single(viewModel.Entries);
        viewModel.ClearFiltersCommand.Execute(null);

        Assert.False(viewModel.SavedEntryHidden);
        Assert.Contains(viewModel.SelectedEntry, viewModel.Entries);
    }

    [Fact]
    public async Task SaveFailure_KeepsDraftAndAllowsRetry()
    {
        var vault = new TestVault();
        vault.Fill();
        vault.UserRepository.Setup(repository => repository.AddAsync(It.IsAny<User>())).ThrowsAsync(new IOException());

        await vault.ViewModel.SaveEntryCommand.ExecuteAsync(null);

        Assert.True(vault.ViewModel.IsEditorOpen);
        Assert.True(vault.ViewModel.Editor.IsDirty);
        Assert.Equal("  exact secret  ", vault.ViewModel.Editor.Password);
        Assert.True(vault.ViewModel.Notification.IsError);
        Assert.True(vault.ViewModel.CanSave);
    }

    [Fact]
    public async Task MissingEntry_DoesNotReportSuccessfulEdit()
    {
        var vault = new TestVault(new User("Site", "login", "pw"));
        var viewModel = vault.ViewModel;

        await viewModel.LoadEntriesAsync();
        viewModel.SelectedEntry = viewModel.Entries[0];
        viewModel.EditEntryCommand.Execute(null);
        viewModel.Editor.Login = "updated";
        vault.Users.Clear();

        await viewModel.SaveEntryCommand.ExecuteAsync(null);

        Assert.True(viewModel.Notification.IsError);
        Assert.True(viewModel.IsEditorOpen);
        Assert.Equal("updated", viewModel.Editor.Login);
        vault.UserRepository.Verify(repository => repository.UpdateAsync(It.IsAny<User>()), Times.Never);

        Assert.True(viewModel.EntryMissing);
        await viewModel.SaveAsNewCommand.ExecuteAsync(null);

        Assert.Single(vault.Users);
        Assert.Single(viewModel.Entries);
        Assert.Equal("updated", viewModel.SelectedEntry!.Login);
        Assert.False(viewModel.EntryMissing);
    }

    [Fact]
    public void CancelDirtyEditor_KeepOrDiscard_IsExplicit()
    {
        var vault = new TestVault();
        vault.Fill();
        var viewModel = vault.ViewModel;
        viewModel.Editor.IsPasswordVisible = true;
        viewModel.CancelEditingCommand.Execute(null);

        Assert.True(viewModel.IsDiscardPending);
        Assert.False(viewModel.Editor.IsPasswordVisible);
        viewModel.KeepEditingCommand.Execute(null);

        Assert.True(viewModel.IsEditorOpen);
        Assert.Equal("Personal account", viewModel.Editor.Site);
        viewModel.CancelEditingCommand.Execute(null);
        viewModel.DiscardChangesCommand.Execute(null);

        Assert.False(viewModel.IsEditorOpen);
        Assert.Empty(viewModel.Editor.Password);
        Assert.Empty(vault.Users);
    }

    [Fact]
    public void CloseDirtyWindow_RequiresExplicitDiscard()
    {
        var vault = new TestVault();
        vault.Fill();
        var requested = false;
        vault.ViewModel.CloseRequested += () => requested = true;

        Assert.False(vault.ViewModel.RequestClose());
        Assert.False(requested);
        vault.ViewModel.DiscardChangesCommand.Execute(null);

        Assert.True(requested);
        Assert.Empty(vault.ViewModel.Editor.Password);
    }

    [Fact]
    public async Task Delete_RequiresConfirmation_AndCanBeCanceled()
    {
        var vault = new TestVault(new User("Site", "login", "pw"));
        var viewModel = vault.ViewModel;

        await viewModel.LoadEntriesAsync();
        viewModel.SelectedEntry = viewModel.Entries[0];

        await viewModel.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.Single(vault.Users);
        viewModel.RequestDeleteCommand.Execute(null);
        viewModel.CancelDeleteCommand.Execute(null);

        Assert.Single(vault.Users);
        viewModel.RequestDeleteCommand.Execute(null);

        await viewModel.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.Empty(vault.Users);
        Assert.Null(viewModel.SelectedEntry);
        Assert.True(viewModel.IsVaultEmpty);
        Assert.False(viewModel.Notification.IsError);
    }

    [Fact]
    public async Task DeleteFailure_KeepsEntryAndConfirmation()
    {
        var vault = new TestVault(new User("Site", "login", "pw"));
        var viewModel = vault.ViewModel;

        await viewModel.LoadEntriesAsync();
        viewModel.SelectedEntry = viewModel.Entries[0];
        vault.UserRepository.Setup(repository => repository.DeleteAsync(It.IsAny<User>())).ThrowsAsync(new IOException());
        viewModel.RequestDeleteCommand.Execute(null);

        await viewModel.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsDeletePending);
        Assert.Single(viewModel.Entries);
        Assert.True(viewModel.Notification.IsError);
    }

    [Fact]
    public async Task BusySave_PreventsConcurrentMutation()
    {
        var vault = new TestVault();
        vault.Fill();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vault.UserRepository.Setup(repository => repository.AddAsync(It.IsAny<User>())).Returns(pending.Task);
        var saving = vault.ViewModel.SaveEntryCommand.ExecuteAsync(null);

        Assert.True(vault.ViewModel.IsBusy);
        Assert.False(vault.ViewModel.SaveEntryCommand.CanExecute(null));
        vault.ViewModel.NewEntryCommand.Execute(null);

        Assert.Equal("Personal account", vault.ViewModel.Editor.Site);
        Assert.False(vault.ViewModel.RequestClose());
        pending.SetResult();

        await saving;
        vault.UserRepository.Verify(repository => repository.AddAsync(It.IsAny<User>()), Times.Once);

        Assert.False(vault.ViewModel.IsBusy);
    }

    [Fact]
    public async Task Reload_PreservesSelectionById_AndMasksIt()
    {
        var viewModel = new TestVault(new User("Site", "login", "pw")).ViewModel;

        await viewModel.LoadEntriesAsync();
        viewModel.SelectedEntry = viewModel.Entries[0];
        var id = viewModel.SelectedEntry.Id;
        viewModel.SelectedEntry.IsPasswordVisible = true;

        await viewModel.LoadEntriesAsync();

        Assert.Equal(id, viewModel.SelectedEntry!.Id);
        Assert.False(viewModel.SelectedEntry.IsPasswordVisible);
    }

    [Fact]
    public async Task LoadFailure_ShowsRetryAndRecovers()
    {
        var vault = new TestVault(new User("Site", "login", "pw"));
        vault.UserRepository.SetupSequence(repository => repository.GetAllAsync()).ThrowsAsync(new IOException()).ReturnsAsync(vault.Users);

        await vault.ViewModel.LoadEntriesAsync();

        Assert.True(vault.ViewModel.HasLoadError);
        Assert.False(vault.ViewModel.IsVaultEmpty);
        await vault.ViewModel.RetryCommand.ExecuteAsync(null);

        Assert.False(vault.ViewModel.HasLoadError);
        Assert.Single(vault.ViewModel.Entries);
    }
}
