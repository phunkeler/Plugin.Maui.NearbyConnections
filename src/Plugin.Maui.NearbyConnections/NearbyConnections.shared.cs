namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Static access to the Plugin.Maui.NearbyConnections API.
/// </summary>
public static class NearbyConnections
{
    static readonly Lock s_lock = new();

    static INearbyConnections? s_defaultImplementation;
    static NearbyConnectionsOptions? s_defaultOptions;

    internal static void SetDefault(INearbyConnections? implementation) =>
        s_defaultImplementation = implementation;

    /// <summary>
    /// Configure options for the static Default instance.
    /// Must be called before accessing Default for the first time.
    /// </summary>
    /// <param name="configure">Action to configure options</param>
    public static void Configure(Action<NearbyConnectionsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        lock (s_lock)
        {
            if (s_defaultImplementation != null)
            {
                throw new InvalidOperationException(
                    "Cannot configure after Default has been accessed. Call Configure() before using Default.");
            }

            s_defaultOptions = new NearbyConnectionsOptions();
            configure(s_defaultOptions);
        }
    }

    /// <summary>
    /// Provides the default implementation for static usage of this API.
    /// </summary>
    public static INearbyConnections Default
    {
        get
        {
            if (s_defaultImplementation is null)
            {
                lock (s_lock)
                {
                    s_defaultImplementation ??= new NearbyConnectionsImplementation(
                        s_defaultOptions ?? new NearbyConnectionsOptions(),
                        loggerFactory: NullLoggerFactory.Instance);
                }
            }
            return s_defaultImplementation;
        }
    }
}

/// <summary>
/// Central event hub implementation that processes all nearby connections events internally
/// before exposing them externally. Maintains device state and handles cross-platform abstractions.
/// </summary>
partial class NearbyConnectionsImplementation : INearbyConnections
{
    readonly SemaphoreSlim _advertiseSemaphore = new(1, 1);
    readonly SemaphoreSlim _discoverSemaphore = new(1, 1);

    readonly Subject<INearbyConnectionsEvent> _events;
    readonly ConcurrentDictionary<string, NearbyDevice> _devices = new();
    readonly ILoggerFactory _loggerFactory;
    readonly ILogger _logger;
    internal readonly NearbyConnectionsOptions _options;
    bool _isDisposed;

    Advertiser? _advertiser;
    Discoverer? _discoverer;
    string _displayName = DeviceInfo.Current.Name;

    public IObservable<INearbyConnectionsEvent> Events => _events.AsObservable();

    public bool IsAdvertising { get; private set; }
    public bool IsDiscovering { get; private set; }

    public IReadOnlyDictionary<string, NearbyDevice> Devices => (IReadOnlyDictionary<string, NearbyDevice>)_devices;

    public string DisplayName
    {
        get => _displayName;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));
            _displayName = value;
        }
    }

    /// <summary>
    /// Creates a new instance with default options (for static API usage).
    /// </summary>
    public NearbyConnectionsImplementation()
        : this(new NearbyConnectionsOptions(), NullLoggerFactory.Instance)
    {
    }

    /// <summary>
    /// Creates a new instance with specified options (for DI usage).
    /// </summary>
    /// <param name="options">Configuration options</param>
    /// <param name="loggerFactory">Logger factory for creating loggers (uses NullLoggerFactory if not provided)</param>
    public NearbyConnectionsImplementation(
        NearbyConnectionsOptions options,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _options = options;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<NearbyConnectionsImplementation>();
        _events = new Subject<INearbyConnectionsEvent>();
    }

    /// <summary>
    /// Process an event through the internal pipeline before exposing it to external subscribers.
    /// Follows Sentry's pattern of wrapping processor calls in exception handling.
    /// </summary>
    /// <param name="evt">The event to process</param>
    void ProcessEvent(INearbyConnectionsEvent evt)
    {
        if (evt is null)
        {
            return;
        }

        try
        {
            _events.OnNext(evt);
        }
        catch (Exception ex)
        {
            _logger.EventSubscriberException(evt.EventId, ex);
        }
    }


    public async Task StartAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        await _advertiseSemaphore.WaitAsync(cancellationToken);

        try
        {
            if (IsAdvertising)
            {
                _logger.AlreadyAdvertising();
                return;
            }

            // Capture immutable snapshot of display name for this session
            var displayName = _displayName;

            _logger.StartingAdvertising(_options.ServiceName, displayName);

            _advertiser = new Advertiser(this);
            await _advertiser.StartAdvertisingAsync(displayName);
            IsAdvertising = true;

            _logger.AdvertisingStarted();
        }
        catch (Exception ex)
        {
            _logger.AdvertisingStartFailed(ex);
            _advertiser?.Dispose();
            _advertiser = null;
            IsAdvertising = false;
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
            if (IsDiscovering)
            {
                _logger.AlreadyDiscovering();
                return;
            }

            _logger.StartingDiscovery(_options.ServiceName);

            _discoverer = new Discoverer(this);
            await _discoverer.StartDiscoveringAsync();
            IsDiscovering = true;

            _logger.DiscoveryStarted();
        }
        catch (Exception ex)
        {
            _logger.DiscoveryStartFailed(ex);
            _discoverer?.Dispose();
            _discoverer = null;
            IsDiscovering = false;
            throw;
        }
        finally
        {
            _discoverSemaphore.Release();
        }
    }

    public async Task StopAdvertisingAsync()
    {
        await _advertiseSemaphore.WaitAsync();
        try
        {
            if (IsAdvertising)
            {
                _logger.StoppingAdvertising();
                _advertiser?.StopAdvertising();
                _advertiser?.Dispose();
                _advertiser = null;
                IsAdvertising = false;
                _logger.AdvertisingStopped();
            }
        }
        finally
        {
            _advertiseSemaphore.Release();
        }
    }

    public async Task StopDiscoveryAsync()
    {
        await _discoverSemaphore.WaitAsync();
        try
        {
            if (IsDiscovering)
            {
                _logger.StoppingDiscovery();
                _discoverer?.StopDiscovering();
                _discoverer?.Dispose();
                _discoverer = null;
                IsDiscovering = false;
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

            _events?.OnCompleted();
            _events?.Dispose();
        }

        _isDisposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
