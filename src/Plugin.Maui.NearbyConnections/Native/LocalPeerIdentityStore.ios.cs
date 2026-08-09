namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Resolves and memoizes this process's canonical local <see cref="MCPeerID"/> for the
/// lifetime of the process. Not persisted across app launches — <see cref="NearbyDevice.Id"/>
/// is documented as valid only within the current session, so a fresh identity each launch is
/// correct, not a gap.
/// </summary>
sealed partial class LocalPeerIdentityStore
{
    readonly ILogger<LocalPeerIdentityStore> _logger;

    MCPeerID? _localPeerId;
    readonly Lock _localPeerIdLock = new();

    public LocalPeerIdentityStore(ILogger<LocalPeerIdentityStore> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// Returns the process's single canonical local <see cref="MCPeerID"/>, memoized after
    /// its first resolution. The resulting instance is cached for the lifetime of this
    /// <see cref="LocalPeerIdentityStore"/> and returned as-is by every subsequent call,
    /// regardless of the <paramref name="displayName"/> argument passed to those later calls.
    /// This is safe because <see cref="NearbyOptions.DisplayName"/> is one-time startup
    /// configuration that cannot change after initialization (see that property's doc comment),
    /// so within one process every caller already passes the same value.
    /// </summary>
    /// <remarks>
    /// Memoization closes a native-interop lifetime hazard: without a durable managed
    /// reference, a freshly-returned <see cref="MCPeerID"/> wrapper could be collected by the
    /// GC before .NET-for-iOS's toggle-ref mechanism promotes it to a strong root, even while
    /// native code (e.g. <see cref="MCNearbyServiceAdvertiser"/>) still depends on it.
    /// </remarks>
    public MCPeerID GetLocalPeerId(string displayName)
    {
        if (_localPeerId is not null)
        {
            return _localPeerId;
        }

        lock (_localPeerIdLock)
        {
            _localPeerId ??= new MCPeerID(displayName);
            LogCreatedLocalPeer(displayName);
            return _localPeerId;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Created local peer: DisplayName={DisplayName}")]
    partial void LogCreatedLocalPeer(string displayName);
}
