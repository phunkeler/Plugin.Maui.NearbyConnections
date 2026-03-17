namespace Plugin.Maui.NearbyConnections;

internal sealed partial class NearbyConnectionsImplementation : INearbyConnections
{
    readonly INearbyDeviceManager _deviceManager;
    readonly SemaphoreSlim _advertiseSemaphore = new(initialCount: 1, maxCount: 1);
    readonly SemaphoreSlim _discoverSemaphore = new(initialCount: 1, maxCount: 1);

    bool _isDisposed;
    Advertiser? _advertiser;
    Discoverer? _discoverer;

    internal TimeProvider TimeProvider { get; }
    public NearbyConnectionsEvents Events { get; }

    public IReadOnlyList<NearbyDevice> Devices => _deviceManager.Devices;
    public NearbyConnectionsOptions Options { get; }

    [MemberNotNullWhen(true, nameof(_advertiser))]
    public bool IsAdvertising => _advertiser?.IsAdvertising ?? false;

    [MemberNotNullWhen(true, nameof(_discoverer))]
    public bool IsDiscovering => _discoverer?.IsDiscovering ?? false;

    internal NearbyConnectionsImplementation(
        INearbyDeviceManager deviceManager,
        TimeProvider timeProvider,
        NearbyConnectionsEvents events,
        NearbyConnectionsOptions options)
    {
        ArgumentNullException.ThrowIfNull(deviceManager);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(options);

        _deviceManager = deviceManager;
        TimeProvider = timeProvider;
        Events = events;
        Options = options;
    }

    public async Task StartAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        if (IsAdvertising)
        {
            Trace.TraceWarning("{0} - Advertising is already active.", nameof(StartAdvertisingAsync));
            return;
        }

        try
        {
            await _advertiseSemaphore.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            Trace.TraceInformation("{0} - Starting advertising: ServiceId={1}, DisplayName={2}",
                nameof(StartAdvertisingAsync),
                Options.ServiceId,
                Options.DisplayName);

            _advertiser ??= new Advertiser(this);
            await _advertiser.StartAdvertisingAsync();
            cancellationToken.ThrowIfCancellationRequested();

            Trace.TraceInformation("{0} - Advertising started: ServiceId={1}, DisplayName={2}",
                nameof(StartAdvertisingAsync),
                Options.ServiceId,
                Options.DisplayName);
        }
        catch (OperationCanceledException)
        {
            _advertiser?.StopAdvertising();
            throw;
        }
        catch (Exception ex)
        {
            _advertiser?.StopAdvertising();

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
            Trace.TraceWarning("{0} - Discovery is already active.", nameof(StartDiscoveryAsync));
            return;
        }

        try
        {
            await _discoverSemaphore.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            Trace.TraceInformation("{0} - Starting discovery: ServiceId={1}",
                nameof(StartDiscoveryAsync),
                Options.ServiceId);

            _discoverer ??= new Discoverer(this);
            await _discoverer.StartDiscoveringAsync();
            cancellationToken.ThrowIfCancellationRequested();

            Trace.TraceInformation("{0} - Discovery started: ServiceId={1}",
                nameof(StartDiscoveryAsync),
                Options.ServiceId);
        }
        catch (OperationCanceledException)
        {
            _discoverer?.StopDiscovering();
            throw;
        }
        catch (Exception ex)
        {
            _discoverer?.StopDiscovering();

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
            Trace.TraceInformation("{0} - Advertising is not currently active.", nameof(StopAdvertisingAsync));
            return;
        }

        try
        {
            await _advertiseSemaphore.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            Trace.TraceInformation("{0} - Stopping advertising: ServiceId={1}, DisplayName={2}",
                nameof(StopAdvertisingAsync),
                Options.ServiceId,
                Options.DisplayName);

            _advertiser.StopAdvertising();
            cancellationToken.ThrowIfCancellationRequested();

            Trace.TraceInformation("{0} - Advertising stopped: ServiceId={1}, DisplayName={2}",
                nameof(StopAdvertisingAsync),
                Options.ServiceId,
                Options.DisplayName);
        }
        catch (OperationCanceledException)
        {
            _advertiser.StopAdvertising();
            throw;
        }
        catch (Exception ex)
        {
            _advertiser.StopAdvertising();

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
            Trace.TraceWarning("{0} - Discovery is not currently active.", nameof(StopDiscoveryAsync));
            return;
        }

        try
        {
            await _discoverSemaphore.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            Trace.TraceInformation("{0} - Stopping discovery: ServiceId={1}",
                nameof(StopDiscoveryAsync),
                Options.ServiceId);

            _discoverer.StopDiscovering();
            cancellationToken.ThrowIfCancellationRequested();

            Trace.TraceInformation("{0} - Discovery stopped: ServiceId={1}",
                nameof(StopDiscoveryAsync),
                Options.ServiceId);
        }
        catch (OperationCanceledException)
        {
            _discoverer.StopDiscovering();
            throw;
        }
        catch (Exception ex)
        {
            _discoverer.StopDiscovering();

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
        Trace.TraceInformation("{0} - Sending connection request to: Id={1}, DisplayName={2}",
            nameof(RequestConnectionAsync),
            device.Id,
            device.DisplayName);

        return PlatformRequestConnectionAsync(device);
    }

    public Task RespondToConnectionAsync(NearbyDevice device, bool accept)
    {
        var response = accept ? "Accepting" : "Rejecting";
        Trace.TraceInformation("{0} - {1} connection request from: Id={2}, DisplayName={3}",
            nameof(RespondToConnectionAsync),
            response,
            device.Id,
            device.DisplayName);

        return PlatformRespondToConnectionAsync(device, accept);
    }

    public Task SendAsync(
        NearbyDevice device,
        byte[] data,
        IProgress<NearbyTransferProgress>? progress = null,
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

        return PlatformSendAsync(device, data, progress, cancellationToken);
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
            Trace.TraceInformation("Disposing NearbyConnectionsImplementation.");

            try
            {
                _advertiseSemaphore?.Wait(millisecondsTimeout: 200);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Failed to wait for advertise semaphore: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            }

            _advertiser?.StopAdvertising();
            _advertiser?.Dispose();
            _advertiser = null;
            _advertiseSemaphore?.Dispose();

            try
            {
                _discoverSemaphore?.Wait(millisecondsTimeout: 200);
            }
            catch (Exception ex)
            {

                Trace.TraceError($"Failed to wait for discover semaphore: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            }

            _discoverer?.StopDiscovering();
            _discoverer?.Dispose();
            _discoverer = null;
            _discoverSemaphore?.Dispose();

            PlatformDispose();

            _deviceManager.Clear();
            Events.ClearAllHandlers();
        }

        _isDisposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
