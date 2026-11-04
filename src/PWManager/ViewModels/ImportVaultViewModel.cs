using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PWManager.Application.UseCases.Import;
using PWManager.Services.Interfaces;

namespace PWManager.ViewModels;

public partial class ImportVaultViewModel(
    IVaultImportService importService,
    IFilePickerService filePicker) : ViewModelBase
{
    public event Action? Imported;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(CanConfirm), nameof(ShowChooseButton), nameof(ShowConfirmButton))]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    private string _password = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm), nameof(ShowChooseButton), nameof(ShowConfirmButton), nameof(ShowSummary), nameof(ConfirmLabel))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowChooseButton), nameof(ShowConfirmButton), nameof(ShowSummary), nameof(ConfirmLabel))]
    private bool _hasResult;

    [ObservableProperty]
    private string _resultMessage = string.Empty;

    [ObservableProperty]
    private bool _isOpen;

    public bool HasSelection => SourcePath.Length > 0;

    public bool HasError => ErrorMessage.Length > 0;

    public bool CanConfirm => HasSelection && Password.Length > 0 && !IsBusy && !HasResult;

    public bool ShowChooseButton => !HasSelection && !IsBusy && !HasResult;

    public bool ShowConfirmButton => HasSelection && !HasResult;

    public bool ShowSummary => HasResult;

    public string ConfirmLabel => IsBusy ? "Importing…" : "Import and merge";

    [RelayCommand]
    private async Task ChooseAsync()
    {
        if (IsBusy || HasResult)
        {
            return;
        }

        ErrorMessage = string.Empty;
        var result = await filePicker.PickOpenFileAsync("Import vault from backup");

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
        Password = string.Empty;
        IsOpen = true;
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (!CanConfirm)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = await importService.ImportAsync(SourcePath, Password);

            if (result.Status == VaultImportStatus.InvalidPassword)
            {
                ErrorMessage = "The backup password is incorrect.";
                return;
            }

            if (result.Status != VaultImportStatus.Success)
            {
                ErrorMessage = "Unable to import this backup. The current vault was kept.";
                return;
            }

            Password = string.Empty;
            HasResult = true;
            ResultMessage = BuildResultMessage(result);
            Imported?.Invoke();
        }
        catch (Exception)
        {
            ErrorMessage = "Unable to import this backup. The current vault was kept.";
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
        Password = string.Empty;
        ErrorMessage = string.Empty;
        ResultMessage = string.Empty;
        HasResult = false;
    }

    private static string BuildResultMessage(VaultImportResult result)
    {
        var parts = new List<string>();

        if (result.Added > 0)
        {
            parts.Add($"{result.Added} added");
        }

        if (result.Updated > 0)
        {
            parts.Add($"{result.Updated} updated");
        }

        if (result.Unchanged > 0)
        {
            parts.Add($"{result.Unchanged} unchanged");
        }

        if (result.Skipped > 0)
        {
            parts.Add($"{result.Skipped} kept from this vault");
        }

        if (result.Conflicts > 0)
        {
            parts.Add($"{result.Conflicts} conflicts");
        }

        var summary = parts.Count == 0 ? "Nothing new to merge." : $"Merge complete: {string.Join(", ", parts)}.";

        if (result.Conflicts > 0)
        {
            summary += " Conflicting entries kept the version already in this vault.";
        }

        if (!string.IsNullOrWhiteSpace(result.SafetyCopyPath))
        {
            summary += $" Safety copy: {result.SafetyCopyPath}";
        }

        return summary;
    }
}
