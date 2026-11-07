using System;
using Avalonia.Controls;
using PWManager.ViewModels;

namespace PWManager.Views;

public partial class ImportVaultView : UserControl
{
    private DialogFocusBinder? _focusBinder;

    public ImportVaultView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _focusBinder?.Dispose();

        _focusBinder = DataContext is ImportVaultViewModel viewModel
            ? new DialogFocusBinder(
                this,
                viewModel,
                () => viewModel.IsOpen,
                () => viewModel.HasResult
                    ? "CloseImportButton"
                    : viewModel.HasSelection ? "ImportPasswordBox" : "ChooseImportButton",
                nameof(ImportVaultViewModel.IsOpen),
                nameof(ImportVaultViewModel.HasSelection),
                nameof(ImportVaultViewModel.HasResult))
            : null;
    }
}
