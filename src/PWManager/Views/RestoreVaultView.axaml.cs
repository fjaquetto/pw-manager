using System;
using Avalonia.Controls;
using PWManager.ViewModels;

namespace PWManager.Views;

public partial class RestoreVaultView : UserControl
{
    private DialogFocusBinder? _focusBinder;

    public RestoreVaultView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _focusBinder?.Dispose();

        _focusBinder = DataContext is RestoreVaultViewModel viewModel
            ? new DialogFocusBinder(
                this,
                viewModel,
                () => viewModel.IsOpen,
                () => viewModel.HasSelection ? "ConfirmRestoreButton" : "ChooseRestoreButton",
                nameof(RestoreVaultViewModel.IsOpen),
                nameof(RestoreVaultViewModel.HasSelection))
            : null;
    }
}
