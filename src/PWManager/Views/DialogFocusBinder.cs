using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;

namespace PWManager.Views;

internal sealed class DialogFocusBinder : IDisposable
{
    private readonly Control _root;
    private readonly INotifyPropertyChanged _viewModel;
    private readonly Func<bool> _isOpen;
    private readonly Func<string> _targetName;
    private readonly string[] _propertyNames;

    public DialogFocusBinder(
        Control root,
        INotifyPropertyChanged viewModel,
        Func<bool> isOpen,
        Func<string> targetName,
        params string[] propertyNames)
    {
        _root = root;
        _viewModel = viewModel;
        _isOpen = isOpen;
        _targetName = targetName;
        _propertyNames = propertyNames;
        _viewModel.PropertyChanged += OnPropertyChanged;
    }

    public void Dispose() => _viewModel.PropertyChanged -= OnPropertyChanged;

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!_isOpen() || Array.IndexOf(_propertyNames, args.PropertyName) < 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => ControlFocus.Request(_root, _targetName()));
    }
}
