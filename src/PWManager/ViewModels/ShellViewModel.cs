using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using PWManager.Application.UseCases.Settings;
using PWManager.Domain.Abstractions.Infrastructure;
using PWManager.Services;
using PWManager.Services.Interfaces;

namespace PWManager.ViewModels;

// Nothing of the vault exists before a successful unlock, and locking disposes
// it and zeroes the session key.
public partial class ShellViewModel : ViewModelBase
{
    private readonly Func<MainViewModel> _vaultFactory;
    private readonly IVaultSession _session;
    private readonly IAppSettingsService _settings;
    private readonly AppInfo _appInfo;
    private readonly TimeProvider _timeProvider;
    private static readonly TimeSpan ActivityThrottle = TimeSpan.FromSeconds(10);
    private MainViewModel? _vault;
    private Task? _openingTask;
    private CancellationTokenSource? _autoLock;
    private DateTimeOffset _lastActivity;
    private bool _lockPending;
    private bool _retrying;
    private string? _lockNotice;

    public ShellViewModel(
        UnlockViewModel unlock,
        Func<MainViewModel> vaultFactory,
        IVaultSession session,
        IAppSettingsService settings,
        AppInfo appInfo,
        TimeProvider timeProvider)
    {
        Unlock = unlock;
        _vaultFactory = vaultFactory;
        _session = session;
        _settings = settings;
        _appInfo = appInfo;
        _timeProvider = timeProvider;
        Unlock.Unlocked += OnUnlocked;
        Unlock.PropertyChanged += OnUnlockPropertyChanged;
    }

    public UnlockViewModel Unlock { get; }

    public string WindowTitle => $"PWManager {_appInfo.Version}";

    public MainViewModel? Vault
    {
        get => _vault;
        private set => SetProperty(ref _vault, value);
    }

    [ObservableProperty]
    private bool _isOpening;

    [ObservableProperty]
    private bool _isUnlocked;

    public bool ShowUnlock => !IsUnlocked;

    public bool ShowProgress => Unlock.IsBusy || IsOpening;

    public Task? OpeningTask => _openingTask;

    public void NotifyActivity()
    {
        if (!IsUnlocked)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();

        if (!_lockPending && now - _lastActivity < ActivityThrottle)
        {
            return;
        }

        _lastActivity = now;
        _lockPending = false;
        RestartAutoLock();
    }

    public void HandleWindowMinimized()
    {
        if (IsUnlocked && _settings.Current.LockWhenMinimized)
        {
            RequestLock();
        }
    }

    public void RequestLock()
    {
        if (!IsUnlocked)
        {
            return;
        }

        if (_vault?.IsBusy == true)
        {
            _lockPending = true;

            if (!_retrying)
            {
                _retrying = true;
                _ = RetryPendingLockAsync();
            }

            return;
        }

        Lock();
    }

    public void Lock()
    {
        if (!IsUnlocked && _vault is null)
        {
            return;
        }

        CancelAutoLock();
        _lockPending = false;
        _retrying = false;

        var vault = _vault;
        Vault = null;

        if (vault is not null)
        {
            vault.LockRequested -= OnVaultLockRequested;
            vault.Dispose();
        }

        IsUnlocked = false;
        Unlock.Reset();
        Unlock.Notice = _lockNotice ?? string.Empty;
        _lockNotice = null;
        _session.Lock();
    }

    private void OnUnlocked() => _openingTask = OpenVaultAsync();

    public async Task OpenVaultAsync()
    {
        if (_vault is not null || IsUnlocked || IsOpening)
        {
            return;
        }

        IsOpening = true;
        MainViewModel? vault = null;

        try
        {
            vault = _vaultFactory();
            vault.LockRequested += OnVaultLockRequested;
            await vault.LoadEntriesAsync();
        }
        finally
        {
            IsOpening = false;
        }

        Vault = vault;
        IsUnlocked = true;
        _lockPending = false;
        _lockNotice = null;
        Unlock.Notice = string.Empty;
        _lastActivity = _timeProvider.GetUtcNow();
        RestartAutoLock();
    }

    private void OnVaultLockRequested(string? notice)
    {
        _lockNotice = notice;
        RequestLock();
    }

    private void RestartAutoLock()
    {
        CancelAutoLock();

        var minutes = _settings.Current.AutoLockMinutes;

        if (!IsUnlocked || minutes <= 0)
        {
            return;
        }

        _autoLock = new CancellationTokenSource();
        _ = AutoLockLaterAsync(TimeSpan.FromMinutes(minutes), _autoLock.Token);
    }

    private void CancelAutoLock()
    {
        _autoLock?.Cancel();
        _autoLock?.Dispose();
        _autoLock = null;
    }

    private async Task AutoLockLaterAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(timeout, _timeProvider, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            RequestLock();
        }
    }

    private async Task RetryPendingLockAsync()
    {
        try
        {
            while (_lockPending && IsUnlocked && _vault?.IsBusy == true)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), _timeProvider);
            }

            if (_lockPending && IsUnlocked)
            {
                Lock();
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _retrying = false;
        }
    }

    private void OnUnlockPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(UnlockViewModel.IsBusy))
        {
            OnPropertyChanged(nameof(ShowProgress));
        }
    }

    partial void OnIsOpeningChanged(bool value) => OnPropertyChanged(nameof(ShowProgress));

    partial void OnIsUnlockedChanged(bool value) => OnPropertyChanged(nameof(ShowUnlock));
}
