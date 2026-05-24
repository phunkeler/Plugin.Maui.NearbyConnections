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

    void Raise(Action action)
    {
        if (!Options.MarshalEventsToMainThread || !_dispatcher.IsDispatchRequired)
            action();
        else
            _dispatcher.Dispatch(action);
    }

    internal void OnDeviceFound(NearbyDevice device, DateTimeOffset timeStamp)
        => Raise(() =>
        {
            if (!_devicesObservable.Contains(device))
                _devicesObservable.Add(device);
            DeviceFound?.Invoke(this, new DeviceFoundEventArgs(device, timeStamp));
        });

    internal void OnDeviceLost(NearbyDevice device, DateTimeOffset timeStamp)
        => Raise(() =>
        {
            _devicesObservable.Remove(device);
            DeviceLost?.Invoke(this, new DeviceLostEventArgs(device, timeStamp));
        });

    internal void OnDeviceDisconnected(NearbyDevice device, DateTimeOffset timeStamp)
        => Raise(() =>
        {
            _devicesObservable.Remove(device);
            DeviceDisconnected?.Invoke(this, new DeviceDisconnectedEventArgs(device, timeStamp));
        });

    internal void OnConnectionRequested(NearbyDevice device, DateTimeOffset timeStamp)
        => Raise(() =>
        {
            if (!_devicesObservable.Contains(device))
                _devicesObservable.Add(device);
            ConnectionRequested?.Invoke(this, new ConnectionRequestedEventArgs(device, timeStamp));
        });

    internal void OnConnectionResponded(NearbyDevice device, DateTimeOffset timeStamp, bool accepted)
        => Raise(() =>
        {
            if (!accepted && !_deviceManager.TryGetDevice(device.Id, out _))
                _devicesObservable.Remove(device);
            ConnectionResponded?.Invoke(this, new NearbyDeviceRespondedEventArgs(device, timeStamp, accepted));
        });

    internal void OnError(string operation, string errorMessage, DateTimeOffset timeStamp)
        => Raise(() => ErrorOccurred?.Invoke(this, new NearbyConnectionsErrorEventArgs(operation, errorMessage, timeStamp)));

    internal void OnError(string operation, string errorMessage, DateTimeOffset timeStamp, NearbyDevice device)
        => Raise(() => ErrorOccurred?.Invoke(this, new NearbyConnectionsErrorEventArgs(operation, errorMessage, timeStamp, device)));

    internal void OnAdvertisingStateChanged(bool isAdvertising, DateTimeOffset timeStamp)
        => Raise(() => AdvertisingStateChanged?.Invoke(this, new AdvertisingStateChangedEventArgs(isAdvertising, timeStamp)));

    internal void OnDiscoveringStateChanged(bool isDiscovering, DateTimeOffset timeStamp)
        => Raise(() => DiscoveringStateChanged?.Invoke(this, new DiscoveringStateChangedEventArgs(isDiscovering, timeStamp)));

    internal void OnDeviceStateChanged(NearbyDevice device, NearbyDeviceState previousState, DateTimeOffset timeStamp)
        => Raise(() =>
        {
            device.NotifyStateChanged();
            DeviceStateChanged?.Invoke(this, new NearbyDeviceStateChangedEventArgs(device, timeStamp, previousState));
        });

    internal void OnDataReceived(NearbyDevice device, NearbyPayload payload, DateTimeOffset timeStamp)
        => Raise(() =>
        {
            LogIncomingDataReceived(device.Id, device.DisplayName, payload.GetType().Name);
            DataReceived?.Invoke(this, new DataReceivedEventArgs(device, payload, timeStamp));
        });

    internal void OnIncomingTransferProgress(NearbyDevice device, NearbyTransferProgress progress, DateTimeOffset timeStamp)
        => Raise(() => IncomingTransferProgress?.Invoke(this, new DataTransferProgressEventArgs(device, progress, timeStamp)));
}
