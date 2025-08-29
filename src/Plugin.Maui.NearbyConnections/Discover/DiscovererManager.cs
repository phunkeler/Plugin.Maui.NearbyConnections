namespace Plugin.Maui.NearbyConnections.Discover;

/// <summary>
/// Manager interface for handling discovery operations.
/// </summary>
public interface IDiscovererManager
{
    /// <summary>
    /// Starts the discovery process with the specified options.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task StartDiscoveringAsync(DiscoverOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the discovery process if it is currently active.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task StopDiscoveringAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc/>
public class DiscovererManager : IDiscovererManager
{
    readonly IDiscovererFactory _discovererFactory;

    IDiscoverer? _discoverer;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscovererManager"/> class.
    /// </summary>
    /// <param name="discovererFactory"></param>
    public DiscovererManager(IDiscovererFactory discovererFactory)
    {
        ArgumentNullException.ThrowIfNull(discovererFactory);

        _discovererFactory = discovererFactory;
    }

    /// <inheritdoc/>
    public async Task StartDiscoveringAsync(DiscoverOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        await StopDiscoveringAsync(cancellationToken);

        _discoverer = _discovererFactory.CreateDiscoverer();

        await _discoverer.StartDiscoveringAsync(options, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task StopDiscoveringAsync(CancellationToken cancellationToken = default)
    {
        if (_discoverer?.IsDiscovering ?? false)
        {
            await _discoverer.StopDiscoveringAsync(cancellationToken);
            _discoverer.Dispose();
            _discoverer = null;
        }
    }
}