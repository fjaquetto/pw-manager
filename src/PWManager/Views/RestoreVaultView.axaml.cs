using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using PWManager.ViewModels;

namespace PWManager.Views;

public partial class RestoreVaultView : UserControl
{
    private RestoreVaultViewModel? _viewModel;

    public RestoreVaultView()
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

        _viewModel = DataContext as RestoreVaultViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_viewModel?.IsOpen == true
            && args.PropertyName is nameof(RestoreVaultViewModel.IsOpen) or nameof(RestoreVaultViewModel.HasSelection))
        {
            Dispatcher.UIThread.Post(FocusPrimaryAction);
        }
    }

    private void FocusPrimaryAction()
    {
        var target = _viewModel?.HasSelection == true ? "ConfirmRestoreButton" : "ChooseRestoreButton";
        ControlFocus.Request(this, target);
    }
}
