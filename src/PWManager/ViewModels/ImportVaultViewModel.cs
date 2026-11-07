using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PWManager.Application.UseCases.Import;
using PWManager.Services;
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
        var selection = await BackupFileSelector.SelectAsync(filePicker, "Import vault from backup");

        if (selection.IsCanceled)
        {
            return;
        }

        if (!selection.IsSelected)
        {
            SourcePath = string.Empty;
            FileName = string.Empty;
            ErrorMessage = selection.ErrorMessage;
            IsOpen = true;
            return;
        }

        SourcePath = selection.Path;
        FileName = selection.FileName;
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

        AddCount("added", result.Added);
        AddCount("updated", result.Updated);
        AddCount("unchanged", result.Unchanged);
        AddCount("kept from this vault", result.Skipped);
        AddCount("conflicts", result.Conflicts);

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

        void AddCount(string label, int count)
        {
            if (count > 0)
            {
                parts.Add($"{count} {label}");
            }
        }
    }
}
