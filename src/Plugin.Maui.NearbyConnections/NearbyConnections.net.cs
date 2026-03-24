namespace Plugin.Maui.NearbyConnections;

internal sealed partial class NearbyConnectionsImplementation
{
#pragma warning disable CA1822, S2325, S1144, S1172
    static void PlatformDispose() { }

    const string PlatformNotSupportedMessage = "This functionality is not supported in this platform implementation.";

    Task PlatformDisconnectAsync(NearbyDevice device)
        => throw new PlatformNotSupportedException(PlatformNotSupportedMessage);

    Task PlatformRequestConnectionAsync(NearbyDevice device)
        => throw new PlatformNotSupportedException(PlatformNotSupportedMessage);

    Task PlatformRespondToConnectionAsync(NearbyDevice device, bool accept)
        => throw new PlatformNotSupportedException(PlatformNotSupportedMessage);

    Task PlatformSendAsync(
        NearbyDevice device,
        byte[] data,
        CancellationToken cancellationToken) => throw new PlatformNotSupportedException(PlatformNotSupportedMessage);

    Task PlatformSendAsync(
        NearbyDevice device,
        string fileUri,
        IProgress<NearbyTransferProgress>? progress,
        CancellationToken cancellationToken) => throw new PlatformNotSupportedException(PlatformNotSupportedMessage);
#pragma warning restore CA1822, S2325, S1144, S1172
}