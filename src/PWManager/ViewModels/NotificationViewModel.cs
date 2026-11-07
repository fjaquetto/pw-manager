using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PWManager.ViewModels;

public partial class NotificationViewModel(TimeProvider timeProvider) : ViewModelBase, IDisposable
{
    private static readonly TimeSpan FadeInDelay = TimeSpan.FromMilliseconds(16);
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan SuccessDuration = TimeSpan.FromSeconds(3);

    private CancellationTokenSource? _dismissal;
    private CancellationTokenSource? _animation;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private bool _isError;

    [ObservableProperty]
    private double _opacity;

    public void Show(string message, bool isError = false, bool persistent = false)
    {
        CancelDismissal();
        CancelAnimation();

        Message = message;
        IsError = isError;
        IsVisible = true;
        Opacity = 0;

        _animation = new();
        AsyncOperation.Forget(() => FadeInAsync(_animation.Token));

        if (!isError && !persistent)
        {
            _dismissal = new();
            AsyncOperation.Forget(() => DismissLaterAsync(_dismissal.Token));
        }
    }

    private async Task FadeInAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(FadeInDelay, timeProvider, cancellationToken);

            if (!cancellationToken.IsCancellationRequested)
            {
                Opacity = 1;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task DismissLaterAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SuccessDuration, timeProvider, cancellationToken);

            if (!cancellationToken.IsCancellationRequested)
            {
                StartHide();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    [RelayCommand]
    private void Dismiss() => StartHide();

    private void StartHide()
    {
        CancelDismissal();
        CancelAnimation();

        if (!IsVisible)
        {
            return;
        }

        _animation = new();
        AsyncOperation.Forget(() => HideAsync(_animation.Token));
    }

    private async Task HideAsync(CancellationToken cancellationToken)
    {
        Opacity = 0;

        try
        {
            await Task.Delay(FadeDuration, timeProvider, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            IsVisible = false;
        }
    }

    private void CancelDismissal()
    {
        _dismissal?.Cancel();
        _dismissal?.Dispose();
        _dismissal = null;
    }

    private void CancelAnimation()
    {
        _animation?.Cancel();
        _animation?.Dispose();
        _animation = null;
    }

    public void Dispose()
    {
        CancelDismissal();
        CancelAnimation();
        IsVisible = false;
        Opacity = 0;
    }
}
