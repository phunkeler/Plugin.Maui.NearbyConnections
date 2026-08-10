using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Services;

/// <summary>
/// Exposes how many devices are currently connected, for the header chip.
/// </summary>
/// <remarks>
/// A singleton that lives as long as the session, so its watch loop is never cancelled on purpose
/// — unlike a page ViewModel, which passes its navigation token to
/// <see cref="INearbyDevices.Changes"/>.
/// </remarks>
public sealed partial class ConnectionTracker : ObservableObject, IConnectionTracker
{
    readonly INearby _session;
    readonly IDispatcher _dispatcher;
    readonly ILogger<ConnectionTracker> _logger;

    [ObservableProperty]
    public partial int Count { get; private set; }

    public ConnectionTracker(INearby session, IDispatcher dispatcher, ILogger<ConnectionTracker> logger)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);

        _session = session;
        _dispatcher = dispatcher;
        _logger = logger;

        // Devices is the state, so the count is derived rather than tracked. Seed from the current
        // set, then keep it current from the change stream — one loop replaces the collection
        // subscription plus a per-device PropertyChanged handler this used to maintain.
        Recount();

        _ = WatchAsync();
    }

    async Task WatchAsync()
    {
        try
        {
            await foreach (var _ in _session.Devices.Changes)
            {
                // Count is bound to the header chip, so the write has to land on the UI thread.
                await _dispatcher.DispatchAsync(Recount);
            }
        }
        catch (Exception ex)
        {
            // Nothing awaits this loop. Without this the chip would silently freeze at its last
            // value — no exception surfaces anywhere.
            LogWatchEnded(ex);
        }
    }

    void Recount()
        => Count = _session.Devices.Count(d => d.Status is NearbyDeviceStatus.Connected);

    [LoggerMessage(Level = LogLevel.Error, Message = "Connection tracking ended; the connected count is now frozen.")]
    partial void LogWatchEnded(Exception exception);
}
