using System.Security.Cryptography;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Derives the platform-neutral string key used as <see cref="NearbyDevice.Id"/> from a native
/// <see cref="MCPeerID"/>.
/// </summary>
sealed partial class PeerKeyProvider
{
    readonly ILogger<PeerKeyProvider> _logger;

    public PeerKeyProvider(ILogger<PeerKeyProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// Derives a stable, opaque string key from a remote <see cref="MCPeerID"/>.
    /// The key is a hex-encoded truncated SHA-256 of the peer's archived bytes,
    /// which are stable for the lifetime of the peer.
    /// </summary>
    public string PeerKey(MCPeerID peerID)
    {
        if (peerID is null)
        {
            return string.Empty;
        }

        try
        {
            using var data = PeerIdArchive.Archive(peerID);
            var hash = SHA256.HashData([.. data]);
            return Convert.ToHexString(hash[..8]);
        }
        catch (Exception ex)
        {
            LogFailedToDerivePeerKey(peerID.DisplayName, ex.Message);
            return peerID.DisplayName;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to derive peer key for '{DisplayName}', falling back to DisplayName: {Error}")]
    partial void LogFailedToDerivePeerKey(string displayName, string error);
}
