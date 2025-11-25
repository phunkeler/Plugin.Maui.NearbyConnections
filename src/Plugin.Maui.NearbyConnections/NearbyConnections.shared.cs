namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Default implementation of the <see cref="INearbyConnections"/> interface.
/// </summary>
internal sealed partial class NearbyConnections : INearbyConnections
{
    readonly TimeProvider _timeProvider;
    readonly ILogger _logger;

    readonly SemaphoreSlim _advertiseSemaphore = new(initialCount: 1, maxCount: 1);
    readonly SemaphoreSlim _discoverSemaphore = new(initialCount: 1, maxCount: 1);

    internal readonly NearbyConnectionsOptions _options;

    bool _isDisposed;
    Advertiser? _advertiser;
    Discoverer? _discoverer;

    public NearbyConnectionsEvents Events { get; } = new();

    public string DisplayName
    {
        get;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));
            field = value;
        }
    } = DeviceInfo.Current.Name;

    /// <summary>
    /// Creates a new instance with specified options (for DI usage).
    /// </summary>
    /// <param name="options">Configuration options</param>
    /// <param name="loggerFactory">Logger factory for creating loggers (uses NullLoggerFactory if not provided)</param>
    /// <param name="timeProvider">Time provider for timestamps (uses TimeProvider.System if not provided)</param>
    public NearbyConnections(
        NearbyConnectionsOptions options,
        ILoggerFactory loggerFactory,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _options = options;
        _logger = loggerFactory.CreateLogger<NearbyConnections>();
        _timeProvider = timeProvider;
    }

    public async Task StartAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        await _advertiseSemaphore.WaitAsync(cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_advertiser is not null)
            {
                _logger.AlreadyAdvertising();
                return;
            }

            var displayName = DisplayName;

            _logger.StartingAdvertising(_options.ServiceName, displayName);

            _advertiser = new Advertiser(this);
            await _advertiser.StartAdvertisingAsync(displayName);

            cancellationToken.ThrowIfCancellationRequested();
            _logger.AdvertisingStarted();
        }
        catch (Exception ex)
        {
            _logger.AdvertisingStartFailed(ex);
            _advertiser?.Dispose();
            _advertiser = null;
            throw;
        }
        finally
        {
            _advertiseSemaphore.Release();
        }
    }

    public async Task StartDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        await _discoverSemaphore.WaitAsync(cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_discoverer is not null)
            {
                _logger.AlreadyDiscovering();
                return;
            }

            _logger.StartingDiscovery(_options.ServiceName);

            _discoverer = new Discoverer(this);
            await _discoverer.StartDiscoveringAsync();

            cancellationToken.ThrowIfCancellationRequested();
            _logger.DiscoveryStarted();
        }
        catch (Exception ex)
        {
            _logger.DiscoveryStartFailed(ex);
            _discoverer?.Dispose();
            _discoverer = null;
            throw;
        }
        finally
        {
            _discoverSemaphore.Release();
        }
    }

    public async Task StopAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        await _advertiseSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_advertiser is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.StoppingAdvertising();
                _advertiser?.StopAdvertising();
                cancellationToken.ThrowIfCancellationRequested();
                _advertiser?.Dispose();
                _advertiser = null;
                _logger.AdvertisingStopped();
            }
        }
        finally
        {
            _advertiseSemaphore.Release();
        }
    }

    public async Task StopDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        await _discoverSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_discoverer is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.StoppingDiscovery();
                _discoverer?.StopDiscovering();
                cancellationToken.ThrowIfCancellationRequested();
                _discoverer?.Dispose();
                _discoverer = null;
                _logger.DiscoveryStopped();
            }
        }
        finally
        {
            _discoverSemaphore.Release();
        }
    }

    public async Task SendInvitationAsync(NearbyDevice device, CancellationToken cancellationToken = default)
        => await PlatformSendInvitationAsync(device, cancellationToken);

    public async Task AcceptInvitationAsync(NearbyDevice device, CancellationToken cancellationToken = default)
        => await PlatformAcceptInvitationAsync(device, cancellationToken);

    public async Task DeclineInvitationAsync(NearbyDevice device, CancellationToken cancellationToken = default)
        => await PlatformDeclineInvitationAsync(device, cancellationToken);

    public void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            _logger.Disposing();

            try
            {
                _advertiseSemaphore?.Wait(millisecondsTimeout: 200);
            }
            catch (Exception ex)
            {
                _logger.DisposeSemaphoreFailure(ex);
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
                _logger.DisposeSemaphoreFailure(ex);
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
