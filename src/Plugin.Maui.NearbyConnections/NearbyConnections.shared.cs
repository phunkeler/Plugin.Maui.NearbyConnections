namespace Plugin.Maui.NearbyConnections;

internal sealed partial class NearbyConnectionsImplementation : INearbyConnections
{
    readonly INearbyDeviceManager _deviceManager;
    readonly ILogger<NearbyConnectionsImplementation> _logger;
    readonly SemaphoreSlim _advertiseSemaphore = new(initialCount: 1, maxCount: 1);
    readonly SemaphoreSlim _discoverSemaphore = new(initialCount: 1, maxCount: 1);

#if IOS
    internal PeerIdManager PeerIdManager { get; init; } = null!;
#endif

    bool _isDisposed;

    internal TimeProvider TimeProvider { get; }

    public IReadOnlyList<NearbyDevice> Devices => _deviceManager.Devices;
    public NearbyConnectionsOptions Options { get; }

    internal NearbyConnectionsImplementation(
        INearbyDeviceManager deviceManager,
        TimeProvider timeProvider,
        NearbyConnectionsOptions options,
        ILogger<NearbyConnectionsImplementation> logger
#if IOS
        , PeerIdManager peerIdManager
#endif
        )
    {
        ArgumentNullException.ThrowIfNull(deviceManager);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _deviceManager = deviceManager;
        TimeProvider = timeProvider;
        Options = options;
        _logger = logger;
#if IOS
        PeerIdManager = peerIdManager;
#endif
    }

    public async Task StartAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        if (IsAdvertising)
        {
            LogAdvertisingAlreadyActive();
            return;
        }

        try
        {
            await _advertiseSemaphore.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            LogStartingAdvertising(Options.ServiceId, Options.DisplayName);

            await PlatformStartAdvertisingAsync();
            cancellationToken.ThrowIfCancellationRequested();

            LogAdvertisingStarted(Options.ServiceId, Options.DisplayName);
        }
        catch (OperationCanceledException)
        {
            PlatformStopAdvertising();
            throw;
        }
        catch (Exception ex)
        {
            PlatformStopAdvertising();

            throw new NearbyAdvertisingException(
                Options,
                $"Failed to start advertising.",
                ex);
        }
        finally
        {
            _advertiseSemaphore.Release();
        }
    }

    public async Task StartDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        if (IsDiscovering)
        {
            LogDiscoveryAlreadyActive();
            return;
        }

        try
        {
            await _discoverSemaphore.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            LogStartingDiscovery(Options.ServiceId);

            await PlatformStartDiscoveringAsync();
            cancellationToken.ThrowIfCancellationRequested();

            LogDiscoveryStarted(Options.ServiceId);
        }
        catch (OperationCanceledException)
        {
            PlatformStopDiscovering();
            throw;
        }
        catch (Exception ex)
        {
            PlatformStopDiscovering();

            throw new NearbyDiscoveryException(
                Options,
                $"Failed to start discovery.",
                ex);
        }
        finally
        {
            _discoverSemaphore.Release();
        }
    }

    public async Task StopAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAdvertising)
        {
            LogAdvertisingNotActive();
            return;
        }

        try
        {
            await _advertiseSemaphore.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            LogStoppingAdvertising(Options.ServiceId, Options.DisplayName);

            PlatformStopAdvertising();
            cancellationToken.ThrowIfCancellationRequested();

            LogAdvertisingStopped(Options.ServiceId, Options.DisplayName);
        }
        catch (OperationCanceledException)
        {
            PlatformStopAdvertising();
            throw;
        }
        catch (Exception ex)
        {
            PlatformStopAdvertising();

            throw new NearbyAdvertisingException(
                Options,
                "Failed stopping advertising.",
                ex);
        }
        finally
        {
            _advertiseSemaphore.Release();
        }
    }

    public async Task StopDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        if (!IsDiscovering)
        {
            LogDiscoveryNotActive();
            return;
        }

        try
        {
            await _discoverSemaphore.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            LogStoppingDiscovery(Options.ServiceId);

            PlatformStopDiscovering();
            cancellationToken.ThrowIfCancellationRequested();

            LogDiscoveryStopped(Options.ServiceId);
        }
        catch (OperationCanceledException)
        {
            PlatformStopDiscovering();
            throw;
        }
        catch (Exception ex)
        {
            PlatformStopDiscovering();

            throw new NearbyDiscoveryException(
                Options,
                "Failed stopping discovery.",
                ex);
        }
        finally
        {
            _discoverSemaphore.Release();
        }
    }

    public Task DisconnectAsync(NearbyDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        return PlatformDisconnectAsync(device);
    }

    public Task RequestConnectionAsync(NearbyDevice device)
    {
        LogSendingConnectionRequest(device.Id, device.DisplayName);
        return PlatformRequestConnectionAsync(device);
    }

    public Task RespondToConnectionAsync(NearbyDevice device, bool accept)
    {
        LogRespondingToConnectionRequest(device.Id, device.DisplayName, accept);
        return PlatformRespondToConnectionAsync(device, accept);
    }

    public Task SendAsync(
        NearbyDevice device,
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(data);

        if (device.State != NearbyDeviceState.Connected)
        {
            throw new InvalidOperationException(
                $"Cannot send data: device '{device.DisplayName}' is not connected (current state: {device.State}).");
        }

        if (data.Length == 0)
        {
            return Task.CompletedTask;
        }

        return PlatformSendAsync(device, data, cancellationToken);
    }

    public Task SendAsync(
        NearbyDevice device,
        string uri,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        if (device.State != NearbyDeviceState.Connected)
        {
            throw new InvalidOperationException(
                $"Cannot send data: device '{device.DisplayName}' is not connected (current state: {device.State}).");
        }

        return PlatformSendAsync(device, uri, progress, cancellationToken);
    }

    public void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            LogDisposing();

            try
            {
                _advertiseSemaphore?.Wait(millisecondsTimeout: 200);
            }
            catch (Exception ex)
            {
                LogSemaphoreWaitFailed("advertise", ex);
            }

            PlatformStopAdvertising();
            _advertiseSemaphore?.Dispose();

            try
            {
                _discoverSemaphore?.Wait(millisecondsTimeout: 200);
            }
            catch (Exception ex)
            {
                LogSemaphoreWaitFailed("discover", ex);
            }

            PlatformStopDiscovering();
            _discoverSemaphore?.Dispose();

            PlatformDispose();

            _deviceManager.Clear();

            AdvertisingStateChanged = null;
            DiscoveringStateChanged = null;
            DeviceFound = null;
            DeviceLost = null;
            DeviceDisconnected = null;
            ConnectionRequested = null;
            ConnectionResponded = null;
            ErrorOccurred = null;
            DeviceStateChanged = null;
            DataReceived = null;
            IncomingTransferProgress = null;
        }

        _isDisposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
