using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PWManager.Application.UseCases.Maintenance;
using PWManager.Services.Interfaces;

namespace PWManager.ViewModels;

public partial class RestoreVaultViewModel(
    IVaultMaintenanceService maintenance,
    IFilePickerService filePicker) : ViewModelBase
{
    public const string RestoredNotice = "Backup restored. Unlock with the master password of that backup.";

    public event Action<string?>? Restored;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(CanConfirm), nameof(ShowChooseButton), nameof(ShowConfirmButton))]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm), nameof(ShowChooseButton), nameof(ShowConfirmButton), nameof(ConfirmLabel))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string? _safetyCopyPath;

    public bool HasSelection => SourcePath.Length > 0;

    public bool HasError => ErrorMessage.Length > 0;

    public bool CanConfirm => HasSelection && !IsBusy;

    public bool ShowChooseButton => !HasSelection && !IsBusy;

    public bool ShowConfirmButton => HasSelection;

    public string ConfirmLabel => IsBusy ? "Restoring…" : "Replace vault";

    public static string NoticeFor(string? safetyCopyPath) =>
        string.IsNullOrWhiteSpace(safetyCopyPath)
            ? RestoredNotice
            : $"{RestoredNotice} Safety copy: {safetyCopyPath}";

    [RelayCommand]
    private async Task ChooseAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = string.Empty;
        var result = await filePicker.PickOpenFileAsync("Restore vault from backup");

        if (result.IsCanceled)
        {
            return;
        }

        if (!result.IsSuccess)
        {
            SourcePath = string.Empty;
            FileName = string.Empty;
            ErrorMessage = result.ErrorMessage ?? "Unable to open the selected file.";
            IsOpen = true;
            return;
        }

        SourcePath = result.Path!;
        FileName = Path.GetFileName(SourcePath);
        IsOpen = true;
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (IsBusy || !HasSelection)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            SafetyCopyPath = await maintenance.RestoreAsync(SourcePath);
            IsOpen = false;
            SourcePath = string.Empty;
            FileName = string.Empty;
            Restored?.Invoke(SafetyCopyPath);
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            ErrorMessage = exception is NotSupportedException
                ? "This backup was created by a newer PWManager version. Update the app before restoring it."
                : "This file is not a valid PWManager vault, or it is corrupted. The current vault was kept.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = "Unable to read or replace the vault. Check the file and folder permissions, then try again.";
        }
        catch (Exception)
        {
            ErrorMessage = "Unable to restore this backup. The current vault was kept.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsBusy)
        {
            return;
        }

        Reset();
    }

    public void Reset()
    {
        IsOpen = false;
        SourcePath = string.Empty;
        FileName = string.Empty;
        ErrorMessage = string.Empty;
        SafetyCopyPath = null;
    }
}
