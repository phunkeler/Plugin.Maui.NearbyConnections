namespace Plugin.Maui.NearbyConnections;

internal sealed partial class NearbyConnectionsImplementation
{
#pragma warning disable CA1822, S2325, S1144, S1172
    Task PlatformRequestConnectionAsync(NearbyDevice device)
        => throw new PlatformNotSupportedException("This functionality is not supported in this platform implementation.");

    Task PlatformRespondToConnectionAsync(NearbyDevice device, bool accept)
        => throw new PlatformNotSupportedException("This functionality is not supported in this platform implementation.");

    Task PlatformSendAsync(
        NearbyDevice device,
        byte[] data,
        IProgress<NearbyTransferProgress>? progress,
        CancellationToken cancellationToken) => throw new PlatformNotSupportedException("This functionality is not supported in this platform implementation.");


    Task PlatformSendAsync(
        NearbyDevice device,
        string fileUri,
        IProgress<NearbyTransferProgress>? progress,
        CancellationToken cancellationToken) => throw new PlatformNotSupportedException("This functionality is not supported in this platform implementation.");
#pragma warning restore CA1822, S2325, S1144, S1172
}