using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace NearbyChat.ViewModels;

public abstract partial class BasePageViewModel(
    IDispatcher dispatcher,
    IMessenger messenger)
    : ObservableRecipient(messenger), IDisposable
{
    CancellationTokenSource? _navigationCts;
    bool _disposed;

    protected IDispatcher Dispatcher { get; } = dispatcher;

    protected CancellationToken NavigationToken => _navigationCts?.Token ?? CancellationToken.None;

    [RelayCommand]
    protected virtual void NavigatedTo()
    {
        var old = _navigationCts;
        _navigationCts = new CancellationTokenSource();
        old?.Cancel();
        old?.Dispose();
        IsActive = true;
    }

    [RelayCommand]
    protected virtual void NavigatedFrom()
    {
        var old = _navigationCts;
        _navigationCts = null;
        old?.Cancel();
        old?.Dispose();
        IsActive = false;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            var old = _navigationCts;
            _navigationCts = null;
            old?.Cancel();
            old?.Dispose();
        }

        _disposed = true;
    }
}