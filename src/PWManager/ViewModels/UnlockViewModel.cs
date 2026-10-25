using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PWManager.Application.UseCases.Unlock;
using PWManager.Services;
using PWManager.Services.Interfaces;

namespace PWManager.ViewModels;

public partial class UnlockViewModel(IUnlockService unlockService, AppInfo appInfo) : ViewModelBase
{
    public const int MinimumPasswordLength = 8;

    public string VersionText => $"v{appInfo.Version}";

    public string VersionToolTip => appInfo.Commit is null
        ? $"Version {appInfo.Version}"
        : $"Version {appInfo.Version} · build {appInfo.Commit}";

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordMask), nameof(VisibilityLabel))]
    private bool _isPasswordVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ButtonLabel), nameof(IsIdle), nameof(ShowProgress), nameof(CanUnlock))]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle), nameof(ShowProgress), nameof(CanUnlock))]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    private bool _isOpeningVault;

    // Submitting is blocked until the shell resolves the status: a new vault must
    // never be created through the unlock flow, which has no confirmation field.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUnlock))]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    private bool _isPreparing = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCreating), nameof(Heading), nameof(Description), nameof(ButtonLabel))]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    private VaultStatus _status = VaultStatus.Existing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    private string _notice = string.Empty;

    public event Action? Unlocked;

    public bool IsCreating => Status == VaultStatus.New;

    public char PasswordMask => IsPasswordVisible ? '\0' : '•';

    public string VisibilityLabel => IsPasswordVisible ? "Hide password" : "Show password";

    public bool HasError => ErrorMessage.Length > 0;

    public bool HasNotice => Notice.Length > 0;

    public bool IsIdle => !IsBusy && !IsOpeningVault;

    public bool CanUnlock => IsIdle && !IsPreparing;

    public bool ShowProgress => IsBusy || IsOpeningVault;

    public string Heading => IsCreating ? "Create your vault" : "Unlock your vault";

    public string Description => IsCreating
        ? "Choose a master password to encrypt your vault. There is no recovery if you forget it."
        : "Your passwords, right where you left them.";

    public string ButtonLabel => IsBusy
        ? (IsCreating ? "Creating…" : "Unlocking…")
        : (IsCreating ? "Create vault" : "Unlock vault");

    public void Configure(VaultStatus status)
    {
        Status = status;
        IsPreparing = false;
    }

    public void Reset()
    {
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        IsPasswordVisible = false;
        ErrorMessage = string.Empty;
        Notice = string.Empty;
    }

    partial void OnPasswordChanged(string value) => ErrorMessage = string.Empty;

    partial void OnConfirmPasswordChanged(string value) => ErrorMessage = string.Empty;

    [RelayCommand]
    private void TogglePasswordVisibility() => IsPasswordVisible = !IsPasswordVisible;

    [RelayCommand(CanExecute = nameof(CanUnlock))]
    private async Task UnlockAsync()
    {
        if (IsBusy || IsPreparing)
        {
            return;
        }

        if (string.IsNullOrEmpty(Password))
        {
            ErrorMessage = IsCreating ? "Choose a master password." : "Enter your master password.";
            return;
        }

        if (IsCreating && Password.Length < MinimumPasswordLength)
        {
            ErrorMessage = $"Use at least {MinimumPasswordLength} characters.";
            return;
        }

        if (IsCreating && Password != ConfirmPassword)
        {
            ErrorMessage = "The passwords do not match.";
            return;
        }

        IsBusy = true;
        var error = string.Empty;
        var unlocked = false;

        try
        {
            var result = await unlockService.UnlockAsync(Password);

            if (result == UnlockResult.Success)
            {
                unlocked = true;
            }
            else
            {
                error = result == UnlockResult.InvalidPassword
                    ? "Incorrect master password. Please try again."
                    : IsCreating
                        ? "Unable to create your vault. Please try again."
                        : "Unable to open your vault. Please try again.";
            }
        }
        catch (Exception)
        {
            error = IsCreating
                ? "Unable to create your vault. Please try again."
                : "Unable to open your vault. Please try again.";
        }
        finally
        {
            Password = string.Empty;
            ConfirmPassword = string.Empty;
            IsPasswordVisible = false;
            ErrorMessage = error;
            IsBusy = false;
        }

        if (unlocked)
        {
            Unlocked?.Invoke();
        }
    }
}
