namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Static access to the Plugin.Maui.NearbyConnections API.
/// </summary>
public static class NearbyConnections
{
    /// <summary>
    /// Gets the default implementation of the <see cref="INearbyConnections"/> interface.
    /// </summary>
    public static INearbyConnections Current
        => field ??= new NearbyConnectionsImplementation(new NearbyDeviceManager(TimeProvider.System, new NearbyConnectionsEvents()), new NearbyConnectionsEvents());
}

internal sealed partial class NearbyConnectionsImplementation : INearbyConnections
{
    internal TimeProvider TimeProvider { get; } = TimeProvider.System;
    readonly INearbyDeviceManager _deviceManager;
    readonly SemaphoreSlim _advertiseSemaphore = new(initialCount: 1, maxCount: 1);
    readonly SemaphoreSlim _discoverSemaphore = new(initialCount: 1, maxCount: 1);

    bool _isDisposed;
    Advertiser? _advertiser;
    Discoverer? _discoverer;

    public NearbyConnectionsEvents Events { get; }

    internal NearbyConnectionsImplementation(INearbyDeviceManager deviceManager, NearbyConnectionsEvents events)
    {
        ArgumentNullException.ThrowIfNull(deviceManager);
        ArgumentNullException.ThrowIfNull(events);

        _deviceManager = deviceManager;
        Events = events;
    }

    public IReadOnlyList<NearbyDevice> Devices => _deviceManager.Devices;
    public NearbyConnectionsOptions Options { get; set; } = new();

    public bool IsAdvertising => _advertiser?.IsAdvertising ?? false;

    public bool IsDiscovering => _discoverer?.IsDiscovering ?? false;

    public async Task StartAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        if (IsAdvertising)
        {
            Trace.WriteLine("Advertising is already active, skipping StartAdvertisingAsync.");
            return;
        }

        try
        {
            await _advertiseSemaphore.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            Trace.WriteLine($"Starting advertising with ServiceId={Options.ServiceId}, DisplayName={Options.DisplayName}");

            // Create advertiser if needed (kept alive for reuse on Android)
            _advertiser ??= new Advertiser(this);
            await _advertiser.StartAdvertisingAsync();
            cancellationToken.ThrowIfCancellationRequested();
            Trace.WriteLine("Advertising started successfully.");
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
                Options.DisplayName,
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
        // Guard: prevent starting if already discovering
        if (IsDiscovering)
        {
            Trace.WriteLine("Discovery is already active, skipping StartDiscoveryAsync.");
            return;
        }

        try
        {
            await _discoverSemaphore.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            Trace.WriteLine($"Starting discovery with ServiceId={Options.ServiceId}");

            // Create discoverer if needed (kept alive for reuse on Android)
            _discoverer ??= new Discoverer(this);
            await _discoverer.StartDiscoveringAsync();
            cancellationToken.ThrowIfCancellationRequested();
            Trace.WriteLine("Discovery started successfully.");
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
        // Guard: prevent stopping if not advertising
        if (!IsAdvertising)
        {
            Trace.WriteLine("Advertising is not active, skipping StopAdvertisingAsync.");
            return;
        }

        try
        {
            await _advertiseSemaphore.WaitAsync(cancellationToken);

            if (_advertiser is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Trace.WriteLine("Stopping advertising.");
                _advertiser.StopAdvertising();
                cancellationToken.ThrowIfCancellationRequested();
                Trace.WriteLine("Advertising stopped.");
            }
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
                Options.DisplayName,
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
        // Guard: prevent stopping if not discovering
        if (!IsDiscovering)
        {
            Trace.WriteLine("Discovery is not active, skipping StopDiscoveryAsync.");
            return;
        }

        try
        {
            await _discoverSemaphore.WaitAsync(cancellationToken);

            if (_discoverer is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Trace.WriteLine("Stopping discovery.");
                _discoverer.StopDiscovering();
                cancellationToken.ThrowIfCancellationRequested();
                Trace.WriteLine("Discovery stopped.");
            }
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
                "Failed stopping discovery.",
                ex);
        }
        finally
        {
            _discoverSemaphore.Release();
        }
    }

    public Task RequestConnectionAsync(NearbyDevice device)
        => PlatformRequestConnectionAsync(device);

    public Task RespondToConnectionAsync(NearbyDevice device, bool accept)
        => PlatformRespondToConnectionAsync(device, accept);

    public Task SendAsync(
        NearbyDevice device,
        byte[] data,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length == 0)
        {
            return Task.CompletedTask;
        }

        if (device.State != NearbyDeviceState.Connected)
        {
            throw new InvalidOperationException(
                $"Cannot send data: device '{device.DisplayName}' is not connected (current state: {device.State}).");
        }

        return PlatformSendAsync(device, data, progress, cancellationToken);
    }

    public Task SendAsync(
        NearbyDevice device,
        Func<Task<Stream>> streamFactory,
        string streamName,
        IProgress<NearbyTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(streamFactory);

        if (device.State != NearbyDeviceState.Connected)
        {
            throw new InvalidOperationException(
                $"Cannot send data: device '{device.DisplayName}' is not connected (current state: {device.State}).");
        }

        return PlatformSendAsync(device, streamFactory, streamName, progress, cancellationToken);
    }

    public void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            Trace.WriteLine("Disposing NearbyConnectionsImplementation.");

            try
            {
                _advertiseSemaphore?.Wait(millisecondsTimeout: 200);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to wait for advertise semaphore: {ex.Message}");
                Trace.WriteLine(ex.StackTrace);
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

                Trace.WriteLine($"Failed to wait for discover semaphore: {ex.Message}");
                Trace.WriteLine(ex.StackTrace);
            }

            _discoverer?.StopDiscovering();
            _discoverer?.Dispose();
            _discoverer = null;
            _discoverSemaphore?.Dispose();

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
