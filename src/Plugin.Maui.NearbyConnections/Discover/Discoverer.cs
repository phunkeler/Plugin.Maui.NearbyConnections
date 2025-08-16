namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Manages discovering for nearby connections.
/// </summary>
public partial class NearbyConnectionsDiscoverer : IDiscoverer
{
    bool _isDiscovering;

    /// <inheritdoc />
    public event EventHandler<DiscoveringStateChangedEventArgs>? DiscoveringStateChanged;

    /// <inheritdoc />
    public bool IsDiscovering
    {
        get => _isDiscovering;
        private set
        {
            if (_isDiscovering != value)
            {
                _isDiscovering = value;
                DiscoveringStateChanged?.Invoke(this, new DiscoveringStateChangedEventArgs
                {
                    IsDiscovering = value
                });
            }
        }
    }

    /// <inheritdoc />
    public async Task StartDiscoveringAsync(IDiscoveringOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (_isDiscovering)
            return;

        await PlatformStartDiscovering(options, cancellationToken);
        _isDiscovering = true;
    }

    /// <inheritdoc />
    public async Task StopDiscoveringAsync(CancellationToken cancellationToken = default)
    {
        if (!_isDiscovering)
            return;

        await PlatformStopDiscovering(cancellationToken);
        _isDiscovering = false;
    }
}
