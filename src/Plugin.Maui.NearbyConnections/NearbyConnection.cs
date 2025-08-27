namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Implementation of INearbyConnection that represents an active nearby connection session.
/// </summary>
internal class NearbyConnection : INearbyConnection
{
    readonly INearbyConnectionLifecycleOrchestrator _orchestrator;
    bool _isActive;
    bool _disposed;

    public string Id { get; } = Guid.NewGuid().ToString();
    public NearbyConnectionOptions Options { get; }
    public bool IsActive => _isActive && !_disposed;
    public DateTimeOffset StartedAt { get; }
    public TimeSpan Duration => DateTimeOffset.UtcNow - StartedAt;

    // Events
    public event EventHandler<PeerDiscoveredEventArgs>? PeerDiscovered;
    public event EventHandler<InvitationReceivedEventArgs>? InvitationReceived;
    public event EventHandler<ConnectionProgressEventArgs>? ConnectionProgress;
    public event EventHandler<ConnectionEstablishedEventArgs>? ConnectionEstablished;
    public event EventHandler<ConnectionFailedEventArgs>? ConnectionFailed;
    public event EventHandler<PeerMessageReceivedEventArgs>? MessageReceived;

    public NearbyConnection(NearbyConnectionOptions options, IConnectionLifecycleOrchestrator orchestrator)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        StartedAt = DateTimeOffset.UtcNow;

        // Wire up orchestrator events
        _orchestrator.PeerDiscovered += (s, e) => PeerDiscovered?.Invoke(this, e);
        _orchestrator.InvitationReceived += (s, e) => InvitationReceived?.Invoke(this, e);
        _orchestrator.ConnectionProgress += (s, e) => ConnectionProgress?.Invoke(this, e);
        _orchestrator.ConnectionEstablished += (s, e) => ConnectionEstablished?.Invoke(this, e);
        _orchestrator.ConnectionFailed += (s, e) => ConnectionFailed?.Invoke(this, e);
        _orchestrator.MessageReceived += (s, e) => MessageReceived?.Invoke(this, e);
    }

    public async Task<ConnectionAttempt> BeginConnectionAsync(string peerId, CancellationToken cancellationToken = default)
    {
        ThrowIfInactive();
        return await _orchestrator.BeginConnectionAsync(peerId, cancellationToken);
    }

    public async Task CancelConnectionAsync(string peerId, CancellationToken cancellationToken = default)
    {
        ThrowIfInactive();
        await _orchestrator.CancelConnectionAsync(peerId, cancellationToken);
    }

    public async Task DisconnectFromPeerAsync(string peerId, CancellationToken cancellationToken = default)
    {
        ThrowIfInactive();
        await _orchestrator.DisconnectFromPeerAsync(peerId, cancellationToken);
    }

    public async Task AcceptInvitationAsync(string connectionEndpoint, CancellationToken cancellationToken = default)
    {
        ThrowIfInactive();
        await _orchestrator.AcceptInvitationAsync(connectionEndpoint, cancellationToken);
    }

    public async Task RejectInvitationAsync(string connectionEndpoint, CancellationToken cancellationToken = default)
    {
        ThrowIfInactive();
        await _orchestrator.RejectInvitationAsync(connectionEndpoint, cancellationToken);
    }

    public IReadOnlyList<PeerDevice> GetDiscoveredPeers()
    {
        ThrowIfInactive();
        return _orchestrator.GetDiscoveredPeers();
    }

    public IReadOnlyList<PeerDevice> GetConnectedPeers()
    {
        ThrowIfInactive();
        return _orchestrator.GetConnectedPeers();
    }

    public IReadOnlyList<ConnectionAttempt> GetActiveConnectionAttempts()
    {
        ThrowIfInactive();
        return _orchestrator.GetActiveConnectionAttempts();
    }

    public async Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        ThrowIfInactive();
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var data = System.Text.Encoding.UTF8.GetBytes(message);
        await SendDataAsync(data, cancellationToken);
    }

    public async Task SendDataAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        ThrowIfInactive();
        ArgumentNullException.ThrowIfNull(data);
        await _orchestrator.SendDataAsync(data, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsActive) return;

        await _orchestrator.StopSessionAsync(cancellationToken);
        _isActive = false;
    }

    internal void SetActive(bool active) => _isActive = active;

    private void ThrowIfInactive()
    {
        if (!IsActive)
            throw new InvalidOperationException("Connection session is not active.");
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (IsActive)
        {
            try
            {
                StopAsync().GetAwaiter().GetResult();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}