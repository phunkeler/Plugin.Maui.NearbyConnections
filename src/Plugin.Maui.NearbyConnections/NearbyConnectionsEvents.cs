namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides events for nearby connections activity.
/// </summary>
public sealed class NearbyConnectionsEvents
{
    /// <summary>
    /// Event fired when the advertising state changes.
    /// </summary>
    public event EventHandler<AdvertisingStateChangedEventArgs>? AdvertisingStateChanged;

    /// <summary>
    /// Event fired when the discovering state changes.
    /// </summary>
    public event EventHandler<DiscoveringStateChangedEventArgs>? DiscoveringStateChanged;

    /// <summary>
    /// Event fired when a nearby device is discovered.
    /// </summary>
    public event EventHandler<NearbyConnectionsEventArgs>? DeviceFound;

    /// <summary>
    /// Event fired when a nearby device is lost.
    /// </summary>
    public event EventHandler<NearbyConnectionsEventArgs>? DeviceLost;

    /// <summary>
    /// Event fired when a nearby device is disconnected.
    /// </summary>
    public event EventHandler<NearbyConnectionsEventArgs>? DeviceDisconnected;

    /// <summary>
    /// Event fired when a connection request is received from a nearby device.
    /// </summary>
    public event EventHandler<NearbyConnectionsEventArgs>? ConnectionRequested;

    /// <summary>
    /// Event fired when a connection response is received from a nearby device.
    /// </summary>
    public event EventHandler<NearbyDeviceRespondedEventArgs>? ConnectionResponded;

    /// <summary>
    /// Event fired when an operation fails.
    /// </summary>
    public event EventHandler<NearbyConnectionsErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// Event fired when the state of a nearby device changes.
    /// </summary>
    public event EventHandler<NearbyDeviceStateChangedEventArgs>? DeviceStateChanged;

    internal void OnDeviceFound(NearbyDevice device, DateTimeOffset timeStamp)
        => DeviceFound?.Invoke(this, new NearbyConnectionsEventArgs(device, timeStamp));

    internal void OnDeviceLost(NearbyDevice device, DateTimeOffset timeStamp)
        => DeviceLost?.Invoke(this, new NearbyConnectionsEventArgs(device, timeStamp));

    internal void OnDeviceDisconnected(NearbyDevice device, DateTimeOffset timeStamp)
        => DeviceDisconnected?.Invoke(this, new NearbyConnectionsEventArgs(device, timeStamp));

    internal void OnConnectionRequested(NearbyDevice device, DateTimeOffset timeStamp)
        => ConnectionRequested?.Invoke(this, new NearbyConnectionsEventArgs(device, timeStamp));

    internal void OnConnectionResponded(NearbyDevice device, DateTimeOffset timeStamp, bool accepted)
        => ConnectionResponded?.Invoke(this, new NearbyDeviceRespondedEventArgs(device, timeStamp, accepted));

    internal void OnError(string operation, string errorMessage, DateTimeOffset timeStamp)
        => ErrorOccurred?.Invoke(this, new NearbyConnectionsErrorEventArgs(operation, errorMessage, timeStamp));

    internal void OnAdvertisingStateChanged(bool isAdvertising, DateTimeOffset timeStamp)
        => AdvertisingStateChanged?.Invoke(this, new AdvertisingStateChangedEventArgs(isAdvertising, timeStamp));

    internal void OnDiscoveringStateChanged(bool isDiscovering, DateTimeOffset timeStamp)
        => DiscoveringStateChanged?.Invoke(this, new DiscoveringStateChangedEventArgs(isDiscovering, timeStamp));

    internal void OnDeviceStateChanged(
        NearbyDevice device,
        NearbyDeviceState previousState,
        DateTimeOffset timeStamp)
        => DeviceStateChanged?.Invoke(this,
            new NearbyDeviceStateChangedEventArgs(device, timeStamp, previousState));

    internal void ClearAllHandlers()
    {
        DeviceFound = null;
        DeviceLost = null;
        DeviceDisconnected = null;
        ConnectionRequested = null;
        ConnectionResponded = null;
        ErrorOccurred = null;
        AdvertisingStateChanged = null;
        DiscoveringStateChanged = null;
    }
}