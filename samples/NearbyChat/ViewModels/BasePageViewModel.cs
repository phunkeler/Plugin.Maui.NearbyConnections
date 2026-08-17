using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public abstract partial class BasePageViewModel(
    IDispatcher dispatcher)
    : ObservableObject, IDisposable
{
    CancellationTokenSource? _navigationCts;
    RelativeTimeTicker? _relativeTimeTicker;
    bool _disposed;

    protected IDispatcher Dispatcher { get; } = dispatcher;

    protected CancellationToken NavigationToken => _navigationCts?.Token ?? CancellationToken.None;

    /// <summary>
    /// Re-raises <c>PropertyChanged</c> for every row's <c>ReceivedAt</c> on a timer, so a
    /// "5 min ago" label keeps counting up while the page sits open.
    /// </summary>
    /// <remarks>
    /// A device row is an immutable snapshot, so nothing about it changes as time passes — only the
    /// converter's output does. The timer is the signal that makes the binding re-evaluate. It runs
    /// only while the page shows at least one row, and stops on navigation away.
    /// </remarks>
    /// <param name="rows">The rows to refresh on each tick.</param>
    protected void TrackRelativeTime(IReadOnlyList<NearbyDeviceViewModel> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        _relativeTimeTicker ??= new RelativeTimeTicker(
            Dispatcher,
            TimeSpan.FromSeconds(30),
            () =>
            {
                foreach (var row in rows)
                {
                    row.RefreshRelativeTime();
                }
            });

        _relativeTimeTicker.SetActive(rows.Count >= 1);
    }

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

        _relativeTimeTicker?.SetActive(false);
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

            _relativeTimeTicker?.SetActive(false);
        }

        _disposed = true;
    }
}
