namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Interface for peer ID persistence operations.
/// </summary>
public interface IPeerIdStorage
{
    /// <summary>
    /// Gets the stored display name.
    /// </summary>
    /// <returns>The stored display name, or null if not found.</returns>
    string? GetStoredDisplayName();

    /// <summary>
    /// Gets the stored peer ID data.
    /// </summary>
    /// <returns>The stored peer ID data, or null if not found.</returns>
    NSData? GetStoredPeerIdData();

    /// <summary>
    /// Stores the display name and peer ID data.
    /// </summary>
    /// <param name="displayName"></param>
    /// <param name="peerIdData"></param>
    void StorePeerIdData(string displayName, NSData peerIdData);
}

/// <summary>
/// Interface for archiving/unarchiving MCPeerID objects.
/// </summary>
public interface IPeerIdArchiver
{
    /// <summary>
    /// Unarchives a MCPeerID object from the given NSData.
    /// </summary>
    /// <param name="data"></param>
    /// <returns>The unarchived MCPeerID object, or null if the operation failed.</returns>
    MCPeerID? UnarchivePeerId(NSData data);

    /// <summary>
    /// Archives a MCPeerID object to NSData.
    /// </summary>
    /// <param name="peerId"></param>
    /// <returns>The archived NSData, or null if the operation failed.</returns>
    NSData ArchivePeerId(MCPeerID peerId);
}

/// <summary>
/// Default implementation using NSUserDefaults for persistence.
/// </summary>
public class NSUserDefaultsPeerIdStorage : IPeerIdStorage
{
    const string MY_MCPEERID_DISPLAYNAME_KEY = nameof(MY_MCPEERID_DISPLAYNAME_KEY);
    const string MY_MCPEERID_KEY = nameof(MY_MCPEERID_KEY);

    /// <inheritdoc/>
    public string? GetStoredDisplayName()
        => NSUserDefaults.StandardUserDefaults.StringForKey(MY_MCPEERID_DISPLAYNAME_KEY);

    /// <inheritdoc/>
    public NSData? GetStoredPeerIdData()
        => NSUserDefaults.StandardUserDefaults.DataForKey(MY_MCPEERID_KEY);

    /// <inheritdoc/>
    public void StorePeerIdData(string displayName, NSData peerIdData)
    {
        var defaults = NSUserDefaults.StandardUserDefaults;
        defaults.SetString(displayName, MY_MCPEERID_DISPLAYNAME_KEY);
        defaults.SetValueForKey(peerIdData, new NSString(MY_MCPEERID_KEY));
        defaults.Synchronize();
    }
}

/// <summary>
/// Default implementation using NSKeyedArchiver/NSKeyedUnarchiver.
/// </summary>
public class NSKeyedPeerIdArchiver : IPeerIdArchiver
{
    /// <inheritdoc/>
    public MCPeerID? UnarchivePeerId(NSData data)
    {
        var result = NSKeyedUnarchiver.GetUnarchivedObject(typeof(MCPeerID), data, out var error);

        if (error is not null)
        {
            throw new NSErrorException(error);
        }

        if (result is MCPeerID peerId)
        {
            return peerId;
        }

        throw new InvalidOperationException("Failed to unarchive MCPeerID: Result is null or of wrong type");
    }

    /// <inheritdoc/>
    public NSData ArchivePeerId(MCPeerID peerId)
    {
        var data = NSKeyedArchiver.GetArchivedData(peerId, true, out var error);

        if (error is not null)
        {
            throw new NSErrorException(error);
        }

        return data ?? throw new InvalidOperationException("Failed to archive MCPeerID: Result is null");
    }
}

/// <summary>
/// Helper class for archiving and unarchiving objects.
/// </summary>
public class MyPeerIdManager : IDisposable
{
    private readonly IPeerIdStorage _storage;
    private readonly IPeerIdArchiver _archiver;
    private bool _disposedValue;

    /// <summary>
    /// Initializes a new instance with default dependencies.
    /// </summary>
    public MyPeerIdManager() : this(new NSUserDefaultsPeerIdStorage(), new NSKeyedPeerIdArchiver())
    {
    }

    /// <summary>
    /// Initializes a new instance with custom dependencies for testing.
    /// </summary>
    public MyPeerIdManager(IPeerIdStorage storage, IPeerIdArchiver archiver)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _archiver = archiver ?? throw new ArgumentNullException(nameof(archiver));
    }

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

    /// <inheritdoc/>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Gets or creates a peer ID for the specified display name.
    /// </summary>
    /// <param name="displayName">The display name for the peer.</param>
    /// <returns>The peer ID for the specified display name.</returns>
    // https://developer.apple.com/documentation/multipeerconnectivity/mcpeerid
    public MCPeerID? GetPeerId(string displayName)
    {
        Console.WriteLine($"[MANAGER] GetPeerId called with displayName: '{displayName}'");

        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var storedDisplayName = _storage.GetStoredDisplayName();
        Console.WriteLine($"[MANAGER] Stored display name: '{storedDisplayName}'");

        if (storedDisplayName?.Equals(displayName, StringComparison.OrdinalIgnoreCase) == true)
        {
            Console.WriteLine("[MANAGER] Display names match, attempting to restore existing peer ID");
            // Try to restore existing peer ID
            var peerIdData = _storage.GetStoredPeerIdData();
            if (peerIdData is not null)
            {
                Console.WriteLine($"[MANAGER] Found stored peer ID data, length: {peerIdData.Length} bytes");
                try
                {
                    var restoredPeerId = _archiver.UnarchivePeerId(peerIdData);
                    Console.WriteLine($"[MANAGER] Successfully restored peer ID: {restoredPeerId?.DisplayName}");
                    return restoredPeerId;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MANAGER] ERROR: Failed to restore peer ID: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("[MANAGER] No stored peer ID data found");
            }
        }
        else
        {
            Console.WriteLine("[MANAGER] Display names don't match or no stored name, creating new peer ID");
        }

        // Create new peer ID
        Console.WriteLine($"[MANAGER] Creating new peer ID with display name: '{displayName}'");
        var peerId = new MCPeerID(displayName);
        Console.WriteLine($"[MANAGER] New peer ID created: {peerId.DisplayName}");

        try
        {
            var archivedData = _archiver.ArchivePeerId(peerId);
            Console.WriteLine($"[MANAGER] Peer ID archived to {archivedData.Length} bytes");
            _storage.StorePeerIdData(displayName, archivedData);
            Console.WriteLine("[MANAGER] Peer ID data stored successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MANAGER] ERROR: Failed to archive/store peer ID: {ex.Message}");
        }

        return peerId;
    }

    /// <summary>
    /// Serialize an MCPEerID object.
    /// </summary>
    /// <param name="mCPeerID"></param>
    /// <returns></returns>
    public NSData ArchivePeerId(MCPeerID mCPeerID)
        => _archiver.ArchivePeerId(mCPeerID);
}