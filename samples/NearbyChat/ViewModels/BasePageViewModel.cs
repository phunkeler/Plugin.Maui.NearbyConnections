using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NearbyChat.ViewModels;

public abstract partial class BasePageViewModel(
    IDispatcher dispatcher)
    : ObservableObject, IDisposable
{
    CancellationTokenSource? _navigationCts;
    IDispatcherTimer? _relativeTimeTimer;
    EventHandler? _relativeTimeTick;
    bool _disposed;

    protected IDispatcher Dispatcher { get; } = dispatcher;

    protected CancellationToken NavigationToken => _navigationCts?.Token ?? CancellationToken.None;

    protected void TrackRelativeTime(IReadOnlyList<NearbyDeviceViewModel> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Count == 0)
        {
            StopRelativeTime();
            return;
        }

        if (_relativeTimeTimer is not null)
        {
            return;
        }

        _relativeTimeTick = (_, _) =>
        {
            foreach (var row in rows)
            {
                row.RefreshRelativeTime();
            }
        };

        _relativeTimeTimer = Dispatcher.CreateTimer();
        _relativeTimeTimer.Interval = TimeSpan.FromSeconds(30);
        _relativeTimeTimer.Tick += _relativeTimeTick;
        _relativeTimeTimer.Start();
    }

    void StopRelativeTime()
    {
        if (_relativeTimeTimer is null)
        {
            return;
        }

        _relativeTimeTimer.Stop();
        _relativeTimeTimer.Tick -= _relativeTimeTick;
        _relativeTimeTimer = null;
        _relativeTimeTick = null;
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
        var old = _navigationCts;
        _navigationCts = null;
        old?.Cancel();
        old?.Dispose();

        StopRelativeTime();
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

            StopRelativeTime();
        }

        _disposed = true;
    }
}
