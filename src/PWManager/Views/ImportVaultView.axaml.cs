using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using PWManager.ViewModels;

namespace PWManager.Views;

public partial class ImportVaultView : UserControl
{
    private ImportVaultViewModel? _viewModel;

    public ImportVaultView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as ImportVaultViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ImportVaultViewModel.IsOpen)
            or nameof(ImportVaultViewModel.HasSelection)
            or nameof(ImportVaultViewModel.HasResult))
        {
            Dispatcher.UIThread.Post(FocusPrimaryAction);
        }
    }

    private void FocusPrimaryAction()
    {
        var target = _viewModel switch
        {
            { HasResult: true } => "CloseImportButton",
            { HasSelection: true } => "ImportPasswordBox",
            _ => "ChooseImportButton"
        };

        ControlFocus.Request(this, target);
    }
}
