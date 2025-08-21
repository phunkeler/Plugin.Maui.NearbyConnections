namespace Plugin.Maui.NearbyConnections.Advertise;

/// <inheritdoc/>
public partial class Advertiser : IAdvertiser
{
    bool _isAdvertising;

    /// <inheritdoc />
    public event EventHandler<AdvertisingStateChangedEventArgs>? AdvertisingStateChanged;
    
    /// <summary>
    /// Fired when a connection is initiated from a remote peer.
    /// </summary>
    public event EventHandler<ConnectionInitiatedEventArgs>? ConnectionInitiated;
    
    /// <summary>
    /// Fired when a connection result is received.
    /// </summary>
    public event EventHandler<ConnectionResultEventArgs>? ConnectionResult;
    
    /// <summary>
    /// Fired when a peer disconnects.
    /// </summary>
    public event EventHandler<DisconnectedEventArgs>? Disconnected;
    
    /// <summary>
    /// Fired when an invitation is received (iOS only).
    /// </summary>
    public event EventHandler<InvitationReceivedEventArgs>? InvitationReceived;

    /// <summary>
    /// Raises the ConnectionInitiated event.
    /// </summary>
    /// <param name="endpointId">The endpoint identifier.</param>
    /// <param name="endpointName">The endpoint name.</param>
    protected internal void OnConnectionInitiated(string endpointId, string endpointName)
    {
        ConnectionInitiated?.Invoke(this, new ConnectionInitiatedEventArgs
        {
            EndpointId = endpointId,
            EndpointName = endpointName
        });
    }

    /// <summary>
    /// Raises the ConnectionResult event.
    /// </summary>
    /// <param name="endpointId">The endpoint identifier.</param>
    /// <param name="success">Whether the connection was successful.</param>
    protected internal void OnConnectionResult(string endpointId, bool success)
    {
        ConnectionResult?.Invoke(this, new ConnectionResultEventArgs
        {
            EndpointId = endpointId,
            Success = success
        });
    }

    /// <summary>
    /// Raises the Disconnected event.
    /// </summary>
    /// <param name="endpointId">The endpoint identifier.</param>
    protected internal void OnDisconnected(string endpointId)
    {
        Disconnected?.Invoke(this, new DisconnectedEventArgs
        {
            EndpointId = endpointId
        });
    }

    /// <summary>
    /// Raises the InvitationReceived event and returns the event args with the response.
    /// </summary>
    /// <param name="peerId">The peer identifier.</param>
    /// <param name="displayName">The peer display name.</param>
    /// <returns>The event args containing the response.</returns>
    protected internal InvitationReceivedEventArgs OnInvitationReceived(string peerId, string displayName)
    {
        var args = new InvitationReceivedEventArgs
        {
            PeerId = peerId,
            DisplayName = displayName
        };
        InvitationReceived?.Invoke(this, args);
        return args;
    }

    /// <inheritdoc />
    public bool IsAdvertising
    {
        get => _isAdvertising;
        private set
        {
            if (_isAdvertising != value)
            {
                _isAdvertising = value;
                AdvertisingStateChanged?.Invoke(this, new AdvertisingStateChangedEventArgs
                {
                    IsAdvertising = value
                });
            }
        }
    }

    /// <inheritdoc />
    public async Task StartAdvertisingAsync(AdvertisingOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!IsAdvertising)
        {
            await PlatformStartAdvertising(options, cancellationToken);
            IsAdvertising = true;
        }
    }

    /// <inheritdoc />
    public async Task StopAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        if (IsAdvertising)
        {
            await PlatformStopAdvertising(cancellationToken);
            IsAdvertising = false;
        }
    }
}