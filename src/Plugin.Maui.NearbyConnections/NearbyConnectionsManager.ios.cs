using Foundation;
using MultipeerConnectivity;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
///     Helper class for archiving and unarchiving objects.
/// </summary>
public class NearbyConnectionsManager : IDisposable
{
    private bool _disposedValue;
    private MCPeerID? _peerId;

    /*
        Consumers will need the following entries in their Info.plist:

        <key>NSBonjourServices</key>
        <array>
        <string>_[YOURSERVICETYPE-HERE]._tcp</string>
        <string>_[YOURSERVICETYPE-HERE]._udp</string> (OPTIONAL?)
        </array>
        <key>NSLocalNetworkUsageDescription</key>
        <string>[YOURDESCRIPTION-HERE]</string>

        When configuring "NearbyConnections" we need to know the service type.
    */

    private protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~NearbyConnectionsManager()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    // https://developer.apple.com/documentation/multipeerconnectivity/mcpeerid
    private static MCPeerID? GetPeerId(string displayName)
    {
        MCPeerID? result = null;
        var defaults = NSUserDefaults.StandardUserDefaults;
        var oldDisplayName = defaults.StringForKey("PeerDisplayNameKey");

        if (oldDisplayName?.Equals(displayName, StringComparison.OrdinalIgnoreCase) ?? false)
        {
            // Try to restore existing peer ID
            var peerIdData = defaults.DataForKey("PeerIdKey");

            if (peerIdData is NSData data)
            {
                var data1 = NSKeyedUnarchiver.GetUnarchivedObject(typeof(MCPeerID), data, out var error);

                if (error is not null)
                {
                    throw new NSErrorException(error);
                }
                else if (data1 is MCPeerID peerId)
                {
                    result = peerId;
                }
                else
                {
                    throw new InvalidOperationException("Failed to unarchive MCPeerID: Result is null or of wrong type");
                }
            }
        }
        else
        {
            // Create new peer ID
            var peerId = new MCPeerID(displayName);
            var peerIdData = NSKeyedArchiver.GetArchivedData(peerId, true, out var error);

            if (error is not null)
            {
                throw new InvalidOperationException($"Failed to archive MCPeerID: {error.LocalizedDescription}");
            }

            defaults.SetString(displayName, "PeerDisplayNameKey");
            defaults.SetValueForKey(peerIdData ?? "DEFAULT", new NSString("PeerIdKey"));
            defaults.Synchronize();
            result = peerId;
        }

        return result;

    }
}