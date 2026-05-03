namespace Plugin.Maui.NearbyConnections;

sealed partial class NearbyConnectionsImplementation
{
    /// <inheritdoc/>
    public event EventHandler<AdvertisingStateChangedEventArgs>? AdvertisingStateChanged;

    /// <inheritdoc/>
    public event EventHandler<DiscoveringStateChangedEventArgs>? DiscoveringStateChanged;

    /// <inheritdoc/>
    public event EventHandler<DeviceFoundEventArgs>? DeviceFound;

    /// <inheritdoc/>
    public event EventHandler<DeviceLostEventArgs>? DeviceLost;

    /// <inheritdoc/>
    public event EventHandler<DeviceDisconnectedEventArgs>? DeviceDisconnected;

    /// <inheritdoc/>
    public event EventHandler<ConnectionRequestedEventArgs>? ConnectionRequested;

    /// <inheritdoc/>
    public event EventHandler<NearbyDeviceRespondedEventArgs>? ConnectionResponded;

    /// <inheritdoc/>
    public event EventHandler<NearbyConnectionsErrorEventArgs>? ErrorOccurred;

    /// <inheritdoc/>
    public event EventHandler<NearbyDeviceStateChangedEventArgs>? DeviceStateChanged;

    /// <inheritdoc/>
    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <inheritdoc/>
    public event EventHandler<DataTransferProgressEventArgs>? IncomingTransferProgress;

    internal void OnDeviceFound(NearbyDevice device, DateTimeOffset timeStamp)
        => DeviceFound?.Invoke(this, new DeviceFoundEventArgs(device, timeStamp));

    internal void OnDeviceLost(NearbyDevice device, DateTimeOffset timeStamp)
        => DeviceLost?.Invoke(this, new DeviceLostEventArgs(device, timeStamp));

    internal void OnDeviceDisconnected(NearbyDevice device, DateTimeOffset timeStamp)
        => DeviceDisconnected?.Invoke(this, new DeviceDisconnectedEventArgs(device, timeStamp));

    internal void OnConnectionRequested(NearbyDevice device, DateTimeOffset timeStamp)
        => ConnectionRequested?.Invoke(this, new ConnectionRequestedEventArgs(device, timeStamp));

    internal void OnConnectionResponded(NearbyDevice device, DateTimeOffset timeStamp, bool accepted)
        => ConnectionResponded?.Invoke(this, new NearbyDeviceRespondedEventArgs(device, timeStamp, accepted));

    internal void OnError(string operation, string errorMessage, DateTimeOffset timeStamp)
        => ErrorOccurred?.Invoke(this, new NearbyConnectionsErrorEventArgs(operation, errorMessage, timeStamp));

    internal void OnError(string operation, string errorMessage, DateTimeOffset timeStamp, NearbyDevice device)
        => ErrorOccurred?.Invoke(this, new NearbyConnectionsErrorEventArgs(operation, errorMessage, timeStamp, device));

    internal void OnAdvertisingStateChanged(bool isAdvertising, DateTimeOffset timeStamp)
        => AdvertisingStateChanged?.Invoke(this, new AdvertisingStateChangedEventArgs(isAdvertising, timeStamp));

    internal void OnDiscoveringStateChanged(bool isDiscovering, DateTimeOffset timeStamp)
        => DiscoveringStateChanged?.Invoke(this, new DiscoveringStateChangedEventArgs(isDiscovering, timeStamp));

    internal void OnDeviceStateChanged(NearbyDevice device, NearbyDeviceState previousState, DateTimeOffset timeStamp)
        => DeviceStateChanged?.Invoke(this, new NearbyDeviceStateChangedEventArgs(device, timeStamp, previousState));

    internal void OnDataReceived(NearbyDevice device, NearbyPayload payload, DateTimeOffset timeStamp)
        => DataReceived?.Invoke(this, new DataReceivedEventArgs(device, payload, timeStamp));

    internal void OnIncomingTransferProgress(NearbyDevice device, NearbyTransferProgress progress, DateTimeOffset timeStamp)
        => IncomingTransferProgress?.Invoke(this, new DataTransferProgressEventArgs(device, progress, timeStamp));
}
