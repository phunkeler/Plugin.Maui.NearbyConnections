using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public abstract partial class BasePageViewModel(
    IDispatcher dispatcher)
    : ObservableObject, IDisposable
{
    /// <summary>
    /// Undo actions for every session event this page is currently subscribed to, drained on
    /// navigate-away.
    /// </summary>
    readonly List<Action> _sessionUnsubscribes = [];

    CancellationTokenSource? _navigationCts;
    bool _disposed;

    protected IDispatcher Dispatcher { get; } = dispatcher;

    protected CancellationToken NavigationToken => _navigationCts?.Token ?? CancellationToken.None;

    /// <summary>
    /// Subscribes to an <see cref="INearbyConnections"/> event for as long as this page is on screen,
    /// and detaches automatically on navigate-away.
    /// </summary>
    /// <param name="subscribe">Attaches the handler.</param>
    /// <param name="unsubscribe">Detaches the same handler.</param>
    /// <remarks>
    /// <para>
    /// <strong>Page ViewModels must not use <c>+=</c> directly.</strong> The session is a singleton,
    /// so a handler attached without a matching <c>-=</c> keeps the ViewModel alive for the life of
    /// the app, and navigating back to the page attaches a second one — after five visits every
    /// event fires five times.
    /// </para>
    /// <para>
    /// This replaces the automatic cleanup the old <c>EventsAsync(NavigationToken)</c> streams got
    /// for free by ending their enumeration. Payload consumption needs no equivalent: a
    /// <c>ReceiveAsync</c> loop ends by itself when the connection drops.
    /// </para>
    /// </remarks>
    protected void RegisterSessionSubscription(Action subscribe, Action unsubscribe)
    {
        ArgumentNullException.ThrowIfNull(subscribe);
        ArgumentNullException.ThrowIfNull(unsubscribe);

        subscribe();
        _sessionUnsubscribes.Add(unsubscribe);
    }

    void DetachSessionSubscriptions()
    {
        foreach (var unsubscribe in _sessionUnsubscribes)
        {
            unsubscribe();
        }

        _sessionUnsubscribes.Clear();
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
        // Before the token work: re-entering the page re-subscribes, so leaving must always detach
        // first or subscriptions accumulate one per visit.
        DetachSessionSubscriptions();

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
            DetachSessionSubscriptions();

            var old = _navigationCts;
            _navigationCts = null;
            old?.Cancel();
            old?.Dispose();
        }

        _disposed = true;
    }
}