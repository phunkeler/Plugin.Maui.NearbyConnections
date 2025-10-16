namespace Plugin.Maui.NearbyConnections;

partial class NearbyConnectionsImplementation : INearbyConnections
{
    /// <summary>
    /// Generic .NET implementation - not supported on this platform.
    /// </summary>
    private partial Task PlatformSendDataAsync(string deviceId, byte[] data)
    {
        throw new NotSupportedException("Nearby connections are not supported on this platform");
    }

    /// <summary>
    /// Generic .NET implementation - not supported on this platform.
    /// </summary>
    private partial Task PlatformAcceptConnectionAsync(string deviceId)
    {
        throw new NotSupportedException("Nearby connections are not supported on this platform");
    }

    /// <summary>
    /// Generic .NET implementation - not supported on this platform.
    /// </summary>
    private partial Task PlatformRejectConnectionAsync(string deviceId)
    {
        throw new NotSupportedException("Nearby connections are not supported on this platform");
    }
}