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
        => field ??= new NearbyConnectionsImplementation();
}

/// <summary>
/// Default implementation of the <see cref="INearbyConnections"/> interface.
/// </summary>
internal sealed partial class NearbyConnectionsImplementation : INearbyConnections
{
    readonly TimeProvider _timeProvider = TimeProvider.System;
    readonly SemaphoreSlim _advertiseSemaphore = new(initialCount: 1, maxCount: 1);
    readonly SemaphoreSlim _discoverSemaphore = new(initialCount: 1, maxCount: 1);

    bool _isDisposed;
    Advertiser? _advertiser;
    Discoverer? _discoverer;

    public NearbyConnectionsEvents Events { get; } = new();
    public NearbyConnectionsOptions Options { get; set; } = new();

    public string DisplayName
    {
        get;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));
            field = value;
        }
    } = DeviceInfo.Current.Name;

    public async Task StartAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _advertiseSemaphore.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (_advertiser is not null)
            {
                Trace.WriteLine($"Already advertising; call '{nameof(StopAdvertisingAsync)}' before trying to start advertising again.");
                return;
            }

            Trace.WriteLine($"Starting advertising with ServiceName={Options.ServiceName}, DisplayName={DisplayName}");

            _advertiser = new Advertiser(this);
            await _advertiser.StartAdvertisingAsync(DisplayName);
            cancellationToken.ThrowIfCancellationRequested();
            Trace.WriteLine("Advertising started successfully.");
        }
        catch (OperationCanceledException)
        {
            _advertiser?.Dispose();
            _advertiser = null;
            throw;
        }
        catch (Exception ex)
        {
            _advertiser?.Dispose();
            _advertiser = null;

            throw new NearbyAdvertisingException(
                DisplayName,
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
        try
        {
            await _discoverSemaphore.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (_discoverer is not null)
            {
                Trace.WriteLine($"Already discovering; call '{nameof(StopDiscoveryAsync)}' before trying to start discovery again.");
                return;
            }

            Trace.WriteLine($"Starting discovery with ServiceName={Options.ServiceName}");

            _discoverer = new Discoverer(this);
            await _discoverer.StartDiscoveringAsync();
            cancellationToken.ThrowIfCancellationRequested();
            Trace.WriteLine("Discovery started successfully.");
        }
        catch (OperationCanceledException)
        {
            _discoverer?.Dispose();
            _discoverer = null;
            throw;
        }
        catch (Exception ex)
        {
            _discoverer?.Dispose();
            _discoverer = null;

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
        try
        {
            await _advertiseSemaphore.WaitAsync(cancellationToken);

            if (_advertiser is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Trace.WriteLine("Stopping advertising.");
                _advertiser?.StopAdvertising();
                cancellationToken.ThrowIfCancellationRequested();
                _advertiser?.Dispose();
                _advertiser = null;
                Trace.WriteLine("Advertising stopped.");
            }
        }
        catch (OperationCanceledException)
        {
            _advertiser?.Dispose();
            _advertiser = null;
            throw;
        }
        catch (Exception ex)
        {
            _advertiser?.Dispose();
            _advertiser = null;

            throw new NearbyAdvertisingException(
                DisplayName,
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
        try
        {
            await _discoverSemaphore.WaitAsync(cancellationToken);

            if (_discoverer is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Trace.WriteLine("Stopping discovery.");
                _discoverer?.StopDiscovering();
                cancellationToken.ThrowIfCancellationRequested();
                _discoverer?.Dispose();
                _discoverer = null;
                Trace.WriteLine("Discovery stopped.");
            }
        }
        catch (OperationCanceledException)
        {
            _discoverer?.Dispose();
            _discoverer = null;
            throw;
        }
        catch (Exception ex)
        {
            _discoverer?.Dispose();
            _discoverer = null;

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

    public async Task RequestConnectionAsync(NearbyDevice device)
        => await PlatformRequestConnectionAsync(device);

    public async Task RespondToConnectionAsync(NearbyDevice device, bool accept)
        => await PlatformRespondToConnectionAsync(device, accept);

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
