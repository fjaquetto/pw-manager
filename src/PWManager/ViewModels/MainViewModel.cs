using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PWManager.Application.UseCases.Settings;
using PWManager.Application.UseCases.VaultEntries;
using PWManager.Domain.Vault;
using PWManager.Enums;
using PWManager.Services;
using PWManager.Services.Interfaces;

namespace PWManager.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly IVaultEntryService _vaultEntries;
    private readonly IClipboardService _clipboard;
    private readonly IAppSettingsService _settings;
    private readonly AppInfo _appInfo;
    private readonly TimeProvider _timeProvider;
    private List<PasswordEntryViewModel> _allEntries = new();
    private PasswordEntryViewModel? _selectedEntry;
    private Action? _pendingNavigation;
    private bool _generatorForEditor;
    private bool _isRefreshingList;
    private string? _sensitiveClipboard;
    private CancellationTokenSource? _clipboardClear;
    private CancellationTokenSource? _hidePasswords;

    [ObservableProperty]
    private string _searchGlobal = string.Empty;

    [ObservableProperty]
    private string _filterSite = string.Empty;

    [ObservableProperty]
    private string _filterLogin = string.Empty;

    [ObservableProperty]
    private bool _filtersExpanded;

    [ObservableProperty]
    private int _sortIndex;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasLoadError;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private bool _isGeneratorOpen;

    [ObservableProperty]
    private bool _isThemeSettingsOpen;

    [ObservableProperty]
    private bool _isSecuritySettingsOpen;

    [ObservableProperty]
    private bool _isDeletePending;

    [ObservableProperty]
    private bool _isDiscardPending;

    [ObservableProperty]
    private bool _isCompact;

    [ObservableProperty]
    private bool _detailActive;

    [ObservableProperty]
    private bool _savedEntryHidden;

    [ObservableProperty]
    private bool _entryMissing;

    public ObservableCollection<PasswordEntryViewModel> Entries { get; } = new();

    public EntryEditorViewModel Editor { get; } = new();

    public PasswordGeneratorViewModel Generator { get; } = new();

    public ThemeSettingsViewModel ThemeSettings { get; }

    public SecuritySettingsViewModel SecuritySettings { get; }

    public NotificationViewModel Notification
    { get; }

    public IReadOnlyList<string> SortOptions
    { get; } = new[]
    {
        "Site A–Z",
        "Site Z–A",
        "Login A–Z",
        "Login Z–A",
        "Created: newest",
        "Created: oldest",
        "Updated: newest",
        "Updated: oldest"
    };

    public PasswordEntryViewModel? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (_isRefreshingList)
            {
                return;
            }

            if (ReferenceEquals(value, _selectedEntry))
            {
                return;
            }

            RequestNavigation(() =>
            {
                CloseEditor();
                SetSelection(value);
                DetailActive = value is not null;
            });
            OnPropertyChanged();
        }
    }

    public int EntryCount => _allEntries.Count;

    public PasswordEntryViewModel? ListSelection
    {
        get => _selectedEntry is not null && Entries.Contains(_selectedEntry) ? _selectedEntry : null;

        set => SelectedEntry = value;
    }

    public int FilterCount => new[]
    {
        SearchGlobal,
        FilterSite,
        FilterLogin
    }.Count(s => !string.IsNullOrWhiteSpace(s));

    public bool HasFilters => FilterCount > 0;

    public string FilterLabel => FilterCount == 0 ? "Filters" : $"Filters · {FilterCount}";

    public string CountLabel => HasFilters ? $"{Entries.Count} of {EntryCount} entries" : $"{EntryCount} entries";

    public string VersionText => $"v{_appInfo.Version}";

    public string VersionToolTip => _appInfo.Commit is null
        ? $"Version {_appInfo.Version}"
        : $"Version {_appInfo.Version} · build {_appInfo.Commit}";

    public bool HasModal => IsGeneratorOpen || IsDeletePending || IsDiscardPending || IsThemeSettingsOpen || IsSecuritySettingsOpen;

    public bool CanInteract => !IsBusy && !HasModal;

    public bool CanSelect => CanInteract && !IsEditorOpen;

    public bool CanSave => CanInteract && IsEditorOpen;

    public bool ShowList => !IsCompact || !DetailActive;

    public bool ShowPane => !IsCompact || DetailActive;

    public bool ShowDetails => SelectedEntry is not null && !IsEditorOpen;

    public bool ShowWelcome => SelectedEntry is null && !IsEditorOpen;

    public bool IsVaultEmpty => !IsLoading && !HasLoadError && EntryCount == 0;

    public bool NoResults => !IsLoading && !HasLoadError && EntryCount > 0 && Entries.Count == 0;

    public bool ShowEntries => !IsLoading && !HasLoadError && Entries.Count > 0;

    public string SaveLabel => IsBusy ? "Saving…" : Editor.SaveLabel;

    public string GeneratorUseLabel => _generatorForEditor ? "Use password" : "Use in new entry";

    public event Action<string>? FocusRequested;

    public event Action? CloseRequested;

    public event Action<string?>? LockRequested;

    public MainViewModel(
        IVaultEntryService vaultEntries,
        IClipboardService clipboard,
        TimeProvider timeProvider,
        IAppSettingsService settings,
        ThemeSettingsViewModel themeSettings,
        SecuritySettingsViewModel securitySettings,
        AppInfo appInfo)
    {
        _vaultEntries = vaultEntries;
        _clipboard = clipboard;
        _settings = settings;
        _appInfo = appInfo;
        _timeProvider = timeProvider;
        Notification = new NotificationViewModel(timeProvider);
        ThemeSettings = themeSettings;
        ThemeSettings.CloseRequested += CloseThemeSettings;
        ThemeSettings.Saved += () => Notification.Show("Appearance saved.");
        SecuritySettings = securitySettings;
        SecuritySettings.CloseRequested += CloseSecuritySettings;
        SecuritySettings.VaultRestored += OnVaultRestored;
    }

    private void RefreshState()
    {
        foreach (var name in new[]
        {
            nameof(EntryCount),
            nameof(FilterCount),
            nameof(HasFilters),
            nameof(FilterLabel),
            nameof(CountLabel),
            nameof(HasModal),
            nameof(CanInteract),
            nameof(CanSelect),
            nameof(CanSave),
            nameof(ShowList),
            nameof(ShowPane),
            nameof(ShowDetails),
            nameof(ShowWelcome),
            nameof(IsVaultEmpty),
            nameof(NoResults),
            nameof(ShowEntries),
            nameof(SaveLabel),
            nameof(GeneratorUseLabel)
        })
        {
            OnPropertyChanged(name);
        }

        SaveEntryCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value) => RefreshState();

    partial void OnIsLoadingChanged(bool value) => RefreshState();

    partial void OnHasLoadErrorChanged(bool value) => RefreshState();

    partial void OnIsEditorOpenChanged(bool value) => RefreshState();

    partial void OnIsGeneratorOpenChanged(bool value) => RefreshState();

    partial void OnIsThemeSettingsOpenChanged(bool value) => RefreshState();

    partial void OnIsSecuritySettingsOpenChanged(bool value) => RefreshState();

    partial void OnIsDeletePendingChanged(bool value) => RefreshState();

    partial void OnIsDiscardPendingChanged(bool value) => RefreshState();

    partial void OnIsCompactChanged(bool value) => RefreshState();

    partial void OnDetailActiveChanged(bool value) => RefreshState();

    partial void OnSearchGlobalChanged(string value) => ApplyFilter();

    partial void OnFilterSiteChanged(string value) => ApplyFilter();

    partial void OnFilterLoginChanged(string value) => ApplyFilter();

    partial void OnSortIndexChanged(int value) => ApplyFilter();

    public async Task LoadEntriesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsLoading = true;
        IsBusy = true;
        HasLoadError = false;

        try
        {
            var selectedId = SelectedEntry?.Id;
            var users = await _vaultEntries.LoadAsync();
            var loaded = users.Select(PasswordEntryViewModel.FromModel).ToList();
            _allEntries = loaded;
            SetSelection(loaded.FirstOrDefault(e => e.Id == selectedId));
            ApplyFilter();
        }
        catch (Exception)
        {
            HasLoadError = true;
            Notification.Show("Unable to load your entries. Please try again.", isError: true);
        }
        finally
        {
            IsBusy = false;
            IsLoading = false;
        }
    }

    [RelayCommand]
    private Task RetryAsync()
    {
        Notification.DismissCommand.Execute(null);

        return LoadEntriesAsync();
    }

    private void ApplyFilter()
    {
        var filtered = _allEntries.Where(MatchesActiveFilters);
        var sorted = (EntrySortOrder)SortIndex switch
        {
            EntrySortOrder.SiteDescending => filtered.OrderByDescending(e => e.Site, StringComparer.OrdinalIgnoreCase),
            EntrySortOrder.LoginAscending => filtered.OrderBy(e => e.Login, StringComparer.OrdinalIgnoreCase),
            EntrySortOrder.LoginDescending => filtered.OrderByDescending(e => e.Login, StringComparer.OrdinalIgnoreCase),
            EntrySortOrder.CreatedNewest => filtered.OrderByDescending(e => e.CreationDate),
            EntrySortOrder.CreatedOldest => filtered.OrderBy(e => e.CreationDate),
            EntrySortOrder.UpdatedNewest => filtered.OrderByDescending(e => e.LastUpdated),
            EntrySortOrder.UpdatedOldest => filtered.OrderBy(e => e.LastUpdated),
            _ => filtered.OrderBy(e => e.Site, StringComparer.OrdinalIgnoreCase)
        };
        RefreshListPreservingSelection(sorted.ThenBy(entry => entry.Id));
    }

    private bool MatchesActiveFilters(PasswordEntryViewModel entry)
    {
        var matchesGlobalSearch = MatchesQuery(entry.Site, SearchGlobal) || MatchesQuery(entry.Login, SearchGlobal);

        return matchesGlobalSearch
            && MatchesQuery(entry.Site, FilterSite)
            && MatchesQuery(entry.Login, FilterLogin);
    }

    private static bool MatchesQuery(string text, string query)
    {
        return string.IsNullOrWhiteSpace(query)
            || text.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshListPreservingSelection(IEnumerable<PasswordEntryViewModel> filteredEntries)
    {
        var selection = _selectedEntry;
        _isRefreshingList = true;
        Entries.Clear();

        foreach (var entry in filteredEntries)
        {
            Entries.Add(entry);
        }

        _isRefreshingList = false;
        SetSelection(selection);
        SavedEntryHidden = selection is not null && !Entries.Contains(selection);
        RefreshState();
    }

    [RelayCommand]
    private void ToggleFilters() => FiltersExpanded = !FiltersExpanded;

    [RelayCommand]
    private void ClearFilters()
    {
        FilterLogin = string.Empty;
        FilterSite = string.Empty;
        SearchGlobal = string.Empty;
        SavedEntryHidden = false;

        if (!Notification.IsError)
        {
            Notification.DismissCommand.Execute(null);
        }
    }

    private void SetSelection(PasswordEntryViewModel? entry)
    {
        if (_selectedEntry is not null)
        {
            _selectedEntry.IsPasswordVisible = false;
        }

        _selectedEntry = entry;

        if (entry is not null)
        {
            entry.IsPasswordVisible = false;
        }

        OnPropertyChanged(nameof(SelectedEntry));
        OnPropertyChanged(nameof(ListSelection));
        RefreshState();
    }

    private void RequestNavigation(Action action)
    {
        if (!CanInteract)
        {
            return;
        }

        if (IsEditorOpen && Editor.IsDirty)
        {
            _pendingNavigation = action;
            Editor.IsPasswordVisible = false;
            IsDiscardPending = true;
            FocusRequested?.Invoke("KeepEditing");
            return;
        }

        action();
    }

    [RelayCommand]
    private void NewEntry() => RequestNavigation(() => OpenEditor());

    [RelayCommand]
    private void EditEntry() => RequestNavigation(() =>
    {
        if (SelectedEntry is null)
        {
            return;
        }

        OpenEditor(SelectedEntry);
    });

    private void OpenEditor(PasswordEntryViewModel? entry = null)
    {
        HideSensitiveData();
        EntryMissing = false;
        Editor.Open(entry);
        DetailActive = true;
        IsEditorOpen = true;

        Notification.DismissCommand.Execute(null);
        RefreshState();
        FocusRequested?.Invoke("EditorSite");
    }

    private void CloseEditor()
    {
        Editor.Open();
        EntryMissing = false;
        IsEditorOpen = false;
        HideSensitiveData();
    }

    [RelayCommand]
    private void CancelEditing() => RequestNavigation(() =>
    {
        CloseEditor();
        DetailActive = SelectedEntry is not null;
        Notification.DismissCommand.Execute(null);
        FocusRequested?.Invoke("EntriesList");
    });

    [RelayCommand]
    private void BackToEntries() => RequestNavigation(() =>
    {
        CloseEditor();
        DetailActive = false;
        FocusRequested?.Invoke("EntriesList");
    });

    [RelayCommand]
    private void KeepEditing()
    {
        _pendingNavigation = null;
        IsDiscardPending = false;
        FocusRequested?.Invoke("EditorSite");
    }

    [RelayCommand]
    private void DiscardChanges()
    {
        var pending = _pendingNavigation;
        _pendingNavigation = null;
        IsDiscardPending = false;
        CloseEditor();
        pending?.Invoke();
    }

    public bool RequestClose()
    {
        if (IsBusy)
        {
            return false;
        }

        if (IsGeneratorOpen)
        {
            CloseGenerator();
        }

        if (IsThemeSettingsOpen)
        {
            ThemeSettings.CancelCommand.Execute(null);
        }

        if (IsSecuritySettingsOpen)
        {
            SecuritySettings.CancelCommand.Execute(null);
        }

        if (IsDeletePending)
        {
            CancelDelete();
        }

        if (IsDiscardPending)
        {
            return false;
        }

        if (!IsEditorOpen || !Editor.IsDirty)
        {
            return true;
        }

        RequestNavigation(() => CloseRequested?.Invoke());

        return false;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveEntryAsync()
    {
        if (!CanSave || !Editor.Validate())
        {
            return;
        }

        IsBusy = true;
        Notification.DismissCommand.Execute(null);

        try
        {
            var isEdit = Editor.IsEditing;
            var user = await _vaultEntries.SaveAsync(Editor.Id, Editor.Site, Editor.Login, Editor.Password);

            if (user is null)
            {
                ShowMissingEntryError();
                return;
            }

            SelectCommittedEntry(user);
            ShowSaveSuccess(isEdit);
            FocusRequested?.Invoke("DetailCopyPassword");
        }
        catch (Exception)
        {
            Notification.Show("Unable to save this entry. Your changes are still here. Please try again.", isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowMissingEntryError()
    {
        EntryMissing = true;
        _allEntries.RemoveAll(entry => entry.Id == Editor.Id);
        SetSelection(null);
        ApplyFilter();
        Notification.Show("This entry no longer exists. You can save your changes as a new entry.", isError: true);
    }

    private void SelectCommittedEntry(User user)
    {
        var entry = PasswordEntryViewModel.FromModel(user);
        _allEntries.RemoveAll(e => e.Id == user.Id);
        _allEntries.Add(entry);
        CloseEditor();
        SetSelection(entry);
        DetailActive = true;
        ApplyFilter();
    }

    private void ShowSaveSuccess(bool isEdit)
    {
        var message = SavedEntryHidden
            ? "Entry saved. Your filters hide it from the list."
            : isEdit ? "Changes saved." : "Entry saved.";
        Notification.Show(message, persistent: SavedEntryHidden);
    }

    [RelayCommand]
    private void RequestDelete()
    {
        if (!CanInteract || SelectedEntry is null || IsEditorOpen)
        {
            return;
        }

        HideSensitiveData();
        IsDeletePending = true;
        FocusRequested?.Invoke("CancelDelete");
    }

    [RelayCommand]
    private async Task SaveAsNewAsync()
    {
        if (!CanSave || !EntryMissing)
        {
            return;
        }

        Editor.MakeNew();
        EntryMissing = false;
        RefreshState();

        await SaveEntryAsync();
    }

    [RelayCommand]
    private void CancelDelete()
    {
        if (IsBusy)
        {
            return;
        }

        IsDeletePending = false;
        FocusRequested?.Invoke("DetailDelete");
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        if (IsBusy || !IsDeletePending || SelectedEntry is null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var id = SelectedEntry.Id;
            var deleted = await _vaultEntries.DeleteAsync(id);
            _allEntries.RemoveAll(e => e.Id == id);
            SetSelection(null);
            IsDeletePending = false;
            DetailActive = false;
            ApplyFilter();
            Notification.Show(deleted ? "Entry deleted." : "This entry was already removed.");
            FocusRequested?.Invoke("EntriesList");
        }
        catch (Exception)
        {
            Notification.Show("Unable to delete this entry. Please try again.", isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CopyPasswordAsync(PasswordEntryViewModel? entry)
    {
        if (!IsBusy && entry is not null)
        {
            await CopyTextAsync(entry.Password, "Password", sensitive: true);
        }
    }

    [RelayCommand]
    private async Task CopyLoginAsync()
    {
        if (SelectedEntry is not null)
        {
            await CopyTextAsync(SelectedEntry.Login, "Login");
        }
    }

    [RelayCommand]
    private Task CopyGeneratedAsync() => CopyTextAsync(Generator.GeneratedPassword, "Password", sensitive: true);

    public async Task CopyTextAsync(string text, string label = "Content", bool sensitive = false)
    {
        try
        {
            var copied = await _clipboard.SetTextAsync(text);

            if (!copied)
            {
                Notification.Show("Clipboard is unavailable. Please try again.", isError: true);
                return;
            }

            if (sensitive)
            {
                _sensitiveClipboard = text;
                ScheduleClipboardClear();
                var seconds = _settings.Current.ClipboardClearSeconds;
                Notification.Show(seconds > 0 ? $"{label} copied. Clipboard clears in {seconds}s." : $"{label} copied.");
            }
            else
            {
                Notification.Show($"{label} copied.");
            }
        }
        catch (Exception)
        {
            Notification.Show("Unable to copy. Please try again.", isError: true);
        }
    }

    // Only clears the clipboard while it still holds our secret; content the user
    // copied elsewhere in the meantime must survive.
    public async Task ClearSensitiveClipboardAsync(bool notify = true)
    {
        CancelClipboardClear();

        var pending = _sensitiveClipboard;
        _sensitiveClipboard = null;

        if (pending is null)
        {
            return;
        }

        try
        {
            var current = await _clipboard.TryGetTextAsync();

            if (current == pending)
            {
                await _clipboard.ClearAsync();

                if (notify)
                {
                    Notification.Show("Clipboard cleared.");
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private void ScheduleClipboardClear()
    {
        CancelClipboardClear();

        var seconds = _settings.Current.ClipboardClearSeconds;

        if (seconds <= 0)
        {
            return;
        }

        _clipboardClear = new CancellationTokenSource();
        _ = ClearClipboardLaterAsync(TimeSpan.FromSeconds(seconds), _clipboardClear.Token);
    }

    private async Task ClearClipboardLaterAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, _timeProvider, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            await ClearSensitiveClipboardAsync();
        }
    }

    private void CancelClipboardClear()
    {
        _clipboardClear?.Cancel();
        _clipboardClear?.Dispose();
        _clipboardClear = null;
    }

    [RelayCommand]
    private void ToggleDetailPassword()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        SelectedEntry.IsPasswordVisible = !SelectedEntry.IsPasswordVisible;

        if (SelectedEntry.IsPasswordVisible)
        {
            SchedulePasswordHide();
        }
        else
        {
            CancelPasswordHide();
        }
    }

    private void SchedulePasswordHide()
    {
        CancelPasswordHide();

        var seconds = _settings.Current.HidePasswordsAfterSeconds;

        if (seconds <= 0)
        {
            return;
        }

        _hidePasswords = new CancellationTokenSource();
        _ = HidePasswordsLaterAsync(TimeSpan.FromSeconds(seconds), _hidePasswords.Token);
    }

    private async Task HidePasswordsLaterAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, _timeProvider, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            HideSensitiveData();
        }
    }

    private void CancelPasswordHide()
    {
        _hidePasswords?.Cancel();
        _hidePasswords?.Dispose();
        _hidePasswords = null;
    }

    [RelayCommand]
    private void OpenGenerator()
    {
        if (!CanInteract)
        {
            return;
        }

        HideSensitiveData();
        _generatorForEditor = false;
        IsGeneratorOpen = true;
        RefreshState();
        FocusRequested?.Invoke("GeneratedPassword");
    }

    [RelayCommand]
    private void OpenEditorGenerator()
    {
        if (!CanInteract || !IsEditorOpen)
        {
            return;
        }

        OpenGenerator();
        _generatorForEditor = true;
        RefreshState();
    }

    [RelayCommand]
    private void CloseGenerator()
    {
        IsGeneratorOpen = false;
        Generator.IsPasswordVisible = false;
        FocusRequested?.Invoke(_generatorForEditor ? "EditorPassword" : "OpenGenerator");
    }

    [RelayCommand]
    private void OpenThemeSettings()
    {
        if (!CanInteract)
        {
            return;
        }

        HideSensitiveData();
        Notification.DismissCommand.Execute(null);
        ThemeSettings.Open();
        IsThemeSettingsOpen = true;
        RefreshState();
        FocusRequested?.Invoke("ProfileSelector");
    }

    private void CloseThemeSettings()
    {
        IsThemeSettingsOpen = false;
        RefreshState();
        FocusRequested?.Invoke(IsEditorOpen ? "EditorSite" : "OpenThemeSettings");
    }

    [RelayCommand]
    private void OpenSecuritySettings()
    {
        if (!CanInteract)
        {
            return;
        }

        HideSensitiveData();
        Notification.DismissCommand.Execute(null);
        SecuritySettings.Open();
        IsSecuritySettingsOpen = true;
        RefreshState();
        FocusRequested?.Invoke("SecurityAutoLock");
    }

    private void CloseSecuritySettings()
    {
        IsSecuritySettingsOpen = false;
        RefreshState();
        FocusRequested?.Invoke(IsEditorOpen ? "EditorSite" : "OpenSecuritySettings");
    }

    private void OnVaultRestored()
    {
        CloseSecuritySettings();
        LockRequested?.Invoke("Backup restored. Unlock with the master password of that backup.");
    }

    [RelayCommand]
    private void RequestLock() => LockRequested?.Invoke(null);

    [RelayCommand]
    private void UseGeneratedPassword()
    {
        if (!IsGeneratorOpen || IsBusy)
        {
            return;
        }

        var password = Generator.GeneratedPassword;
        var inEditor = _generatorForEditor && IsEditorOpen;
        CloseGenerator();

        if (inEditor)
        {
            Editor.Password = password;
            Editor.IsPasswordVisible = false;
            FocusRequested?.Invoke("EditorPassword");
        }
        else
        {
            RequestNavigation(() =>
            {
                Editor.Open();
                Editor.Password = password;
                DetailActive = true;
                IsEditorOpen = true;
                RefreshState();
                FocusRequested?.Invoke("EditorSite");
            });
        }
    }

    [RelayCommand]
    private void Escape()
    {
        if (IsBusy)
        {
            return;
        }

        if (IsSecuritySettingsOpen)
        {
            SecuritySettings.CancelCommand.Execute(null);
        }
        else if (IsThemeSettingsOpen)
        {
            ThemeSettings.CancelCommand.Execute(null);
        }
        else if (IsGeneratorOpen)
        {
            CloseGenerator();
        }
        else if (IsDeletePending)
        {
            CancelDelete();
        }
        else if (IsDiscardPending)
        {
            KeepEditing();
        }
        else if (IsEditorOpen)
        {
            CancelEditing();
        }
        else if (IsCompact && DetailActive)
        {
            BackToEntries();
        }
        else if (FiltersExpanded)
        {
            FiltersExpanded = false;
        }
    }

    public void HideSensitiveData()
    {
        CancelPasswordHide();

        foreach (var entry in _allEntries)
        {
            entry.IsPasswordVisible = false;
        }

        Editor.IsPasswordVisible = false;
        Generator.IsPasswordVisible = false;
    }

    public void Dispose()
    {
        Notification.Dispose();
        HideSensitiveData();
        CancelClipboardClear();
        _ = ClearSensitiveClipboardAsync(notify: false);
        Editor.Open();
        Generator.GeneratedPassword = string.Empty;
        _allEntries.Clear();
        Entries.Clear();
        SetSelection(null);
    }
}
