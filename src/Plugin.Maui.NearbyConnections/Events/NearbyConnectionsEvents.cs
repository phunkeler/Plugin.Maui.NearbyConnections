namespace Plugin.Maui.NearbyConnections.Events;

/// <inheritdoc/>
public class NearbyConnectionsEvents : INearbyConnectionsEvents
{
    readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates a new instance of <see cref="NearbyConnectionsEvents"/>.
    /// </summary>
    /// <param name="timeProvider"></param>
    public NearbyConnectionsEvents(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Event fired when a nearby device is discovered.
    /// </summary>
    public event EventHandler<NearbyDeviceFoundEventArgs>? DeviceFound;

    /// <summary>
    /// Event fired when a nearby device is lost.
    /// </summary>
    public event EventHandler<NearbyDeviceLostEventArgs>? DeviceLost;

    /// <summary>
    /// Event fired when a nearby device is disconnected.
    /// </summary>
    public event EventHandler<NearbyDeviceDisconnectedEventArgs>? DeviceDisconnected;

    /// <summary>
    /// Event fired when a connection request is received from a nearby device.
    /// </summary>
    public event EventHandler<NearbyConnectionRequestEventArgs>? ConnectionRequested;

    /// <summary>
    /// Event fired when a connection response is received from a nearby device.
    /// </summary>
    public event EventHandler<NearbyConnectionResponseEventArgs>? ConnectionResponded;

    internal void OnDeviceFound(NearbyDevice device)
        => DeviceFound?.Invoke(this, new NearbyDeviceFoundEventArgs(device, _timeProvider.GetUtcNow()));

    internal void OnDeviceLost(NearbyDevice device)
        => DeviceLost?.Invoke(this, new NearbyDeviceLostEventArgs(device, _timeProvider.GetUtcNow()));

    internal void OnDeviceDisconnected(NearbyDevice device)
        => DeviceDisconnected?.Invoke(this, new NearbyDeviceDisconnectedEventArgs(device, _timeProvider.GetUtcNow()));

    internal void OnConnectionRequested(NearbyDevice device)
        => ConnectionRequested?.Invoke(this, new NearbyConnectionRequestEventArgs(device, _timeProvider.GetUtcNow()));

    internal void OnConnectionResponded(NearbyDevice device)
        => ConnectionResponded?.Invoke(this, new NearbyConnectionResponseEventArgs(device, _timeProvider.GetUtcNow()));
}