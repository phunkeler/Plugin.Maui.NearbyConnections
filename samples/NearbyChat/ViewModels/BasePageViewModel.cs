using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public abstract partial class BasePageViewModel(
    IDispatcher dispatcher)
    : ObservableObject, IDisposable
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
    }

    [RelayCommand]
    protected virtual void NavigatedFrom()
    {
        // Cancelling ends every `await foreach` over INearbyDevices.Changes that this page started
        // with NavigationToken. That is the whole cleanup story: there is nothing to detach, so
        // there is no unsubscribe to forget.
        var old = _navigationCts;
        _navigationCts = null;
        old?.Cancel();
        old?.Dispose();
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