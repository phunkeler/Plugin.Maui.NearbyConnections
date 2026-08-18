using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Services;

/// <summary>
/// Tracks how many devices are currently connected, for the header chip. Bound as
/// <see cref="System.ComponentModel.INotifyPropertyChanged"/>, which <see cref="ObservableObject"/>
/// supplies.
/// </summary>
/// <remarks>
/// A singleton that lives as long as the session, so its watch loop is never cancelled on purpose
/// — unlike a page ViewModel, which passes its navigation token to
/// <see cref="INearbyDevices.Changes"/>.
/// </remarks>
public sealed partial class ConnectionTracker : ObservableObject
{
    readonly INearby _nearby;
    readonly IDispatcher _dispatcher;
    readonly ILogger<ConnectionTracker> _logger;

    [ObservableProperty]
    public partial int Count { get; private set; }

    public ConnectionTracker(INearby nearby, IDispatcher dispatcher, ILogger<ConnectionTracker> logger)
    {
        ArgumentNullException.ThrowIfNull(nearby);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);

        _nearby = nearby;
        _dispatcher = dispatcher;
        _logger = logger;

        Recount();

        _ = WatchAsync();
    }

    async Task WatchAsync()
    {
        try
        {
            await foreach (var _ in _nearby.Devices.Changes)
            {
                await _dispatcher.DispatchAsync(Recount);
            }
        }
        catch (Exception ex)
        {
            LogWatchEnded(ex);
        }
    }

    void Recount()
        => Count = _nearby.Devices.Count(d => d.Status is NearbyDeviceStatus.Connected);

    [LoggerMessage(Level = LogLevel.Error, Message = "Connection tracking ended; the connected count is now frozen.")]
    partial void LogWatchEnded(Exception exception);
}
