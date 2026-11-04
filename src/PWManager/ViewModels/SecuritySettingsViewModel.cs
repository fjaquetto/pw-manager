using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PWManager.Application.UseCases.Maintenance;
using PWManager.Application.UseCases.Rekey;
using PWManager.Application.UseCases.Settings;
using PWManager.Domain.Settings;
using PWManager.Services.Interfaces;

namespace PWManager.ViewModels;

public partial class SecuritySettingsViewModel : ViewModelBase
{
    private readonly IAppSettingsService _settings;
    private readonly IVaultRekeyService _rekey;
    private readonly IVaultMaintenanceService _maintenance;
    private readonly IFilePickerService _filePicker;
    private bool _suppressPersist;

    public SecuritySettingsViewModel(
        IAppSettingsService settings,
        IVaultRekeyService rekey,
        IVaultMaintenanceService maintenance,
        IFilePickerService filePicker,
        RestoreVaultViewModel? restore = null,
        ImportVaultViewModel? import = null)
    {
        _settings = settings;
        _rekey = rekey;
        _maintenance = maintenance;
        _filePicker = filePicker;
        Restore = restore ?? new RestoreVaultViewModel(maintenance, filePicker);
        Restore.Restored += OnRestored;
        Import = import;

        if (Import is not null)
        {
            Import.Imported += OnImported;
        }
    }

    public event Action? CloseRequested;

    // The shell locks the vault after a restore, so it can be unlocked again
    // with the backup's master password.
    public event Action<string?>? VaultRestored;

    public event Action? VaultImported;

    public RestoreVaultViewModel Restore { get; }

    public ImportVaultViewModel? Import { get; }

    [ObservableProperty]
    private int _autoLockMinutes = AppSettings.DefaultAutoLockMinutes;

    [ObservableProperty]
    private bool _lockWhenMinimized;

    [ObservableProperty]
    private int _clipboardClearSeconds = AppSettings.DefaultClipboardClearSeconds;

    [ObservableProperty]
    private int _hidePasswordsAfterSeconds = AppSettings.DefaultHidePasswordsAfterSeconds;

    [ObservableProperty]
    private string _currentPassword = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPasswordMessage))]
    private string _passwordMessage = string.Empty;

    [ObservableProperty]
    private bool _isChangingPassword;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBackupMessage))]
    private string _backupMessage = string.Empty;

    [ObservableProperty]
    private bool _isBackingUp;

    public bool HasPasswordMessage => PasswordMessage.Length > 0;

    public bool HasBackupMessage => BackupMessage.Length > 0;

    public bool HasImport => Import is not null;

    public void Open()
    {
        _suppressPersist = true;

        var current = _settings.Current;
        AutoLockMinutes = current.AutoLockMinutes;
        LockWhenMinimized = current.LockWhenMinimized;
        ClipboardClearSeconds = current.ClipboardClearSeconds;
        HidePasswordsAfterSeconds = current.HidePasswordsAfterSeconds;

        _suppressPersist = false;

        ClearPasswordFields();
        PasswordMessage = string.Empty;
        BackupMessage = string.Empty;
        Restore.Reset();
        Import?.Reset();
    }

    partial void OnAutoLockMinutesChanged(int value) => Persist();

    partial void OnLockWhenMinimizedChanged(bool value) => Persist();

    partial void OnClipboardClearSecondsChanged(int value) => Persist();

    partial void OnHidePasswordsAfterSecondsChanged(int value) => Persist();

    private void Persist()
    {
        if (_suppressPersist)
        {
            return;
        }

        _settings.Update(new AppSettings
        {
            AutoLockMinutes = AutoLockMinutes,
            LockWhenMinimized = LockWhenMinimized,
            ClipboardClearSeconds = ClipboardClearSeconds,
            HidePasswordsAfterSeconds = HidePasswordsAfterSeconds
        });
    }

    [RelayCommand]
    private async Task ChangeMasterPasswordAsync()
    {
        if (IsChangingPassword)
        {
            return;
        }

        PasswordMessage = string.Empty;

        if (string.IsNullOrEmpty(CurrentPassword))
        {
            PasswordMessage = "Enter your current master password.";
            return;
        }

        if (NewPassword.Length < UnlockViewModel.MinimumPasswordLength)
        {
            PasswordMessage = $"Use at least {UnlockViewModel.MinimumPasswordLength} characters for the new password.";
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            PasswordMessage = "The new passwords do not match.";
            return;
        }

        IsChangingPassword = true;
        var message = string.Empty;

        try
        {
            var result = await _rekey.ChangeMasterPasswordAsync(CurrentPassword, NewPassword);
            message = result switch
            {
                RekeyResult.Success => "Master password changed.",
                RekeyResult.InvalidCurrentPassword => "The current master password is incorrect.",
                _ => "Unable to change the master password. Please try again."
            };
        }
        catch (Exception)
        {
            message = "Unable to change the master password. Please try again.";
        }
        finally
        {
            IsChangingPassword = false;
            ClearPasswordFields();
            PasswordMessage = message;
        }
    }

    [RelayCommand]
    private async Task BackupVaultAsync()
    {
        if (IsBackingUp)
        {
            return;
        }

        BackupMessage = string.Empty;
        var target = await _filePicker.PickSaveFileAsync("Back up vault", $"pwmanager-backup-{DateTime.Now:yyyyMMdd}.db");

        if (target.IsCanceled)
        {
            return;
        }

        if (!target.IsSuccess)
        {
            BackupMessage = target.ErrorMessage ?? "Unable to choose the backup location.";
            return;
        }

        IsBackingUp = true;

        try
        {
            await _maintenance.BackupAsync(target.Path!);
            BackupMessage = $"Backup saved to {target.Path}";
        }
        catch (Exception)
        {
            BackupMessage = "Unable to save the backup. Please try again.";
        }
        finally
        {
            IsBackingUp = false;
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();

    private void OnRestored(string? safetyCopyPath)
    {
        BackupMessage = string.Empty;
        VaultRestored?.Invoke(safetyCopyPath);
    }

    private void OnImported() => VaultImported?.Invoke();

    private void ClearPasswordFields()
    {
        CurrentPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
    }
}
