namespace Plugin.Maui.NearbyConnections;

internal sealed partial class NearbyConnectionsImplementation
{
    Task PlatformRequestConnectionAsync(NearbyDevice device)
        => throw new NotImplementedException();

    Task PlatformRespondToConnectionAsync(NearbyDevice device, bool accept)
        => throw new NotImplementedException();

    Task PlatformSendAsync(
        NearbyDevice device,
        byte[] data,
        IProgress<NearbyTransferProgress>? progress,
        CancellationToken cancellationToken) => throw new NotImplementedException();

    Task PlatformSendAsync(
        NearbyDevice device,
        FileResult fileResult,
        IProgress<NearbyTransferProgress>? progress,
        CancellationToken cancellationToken) => throw new NotImplementedException();
}