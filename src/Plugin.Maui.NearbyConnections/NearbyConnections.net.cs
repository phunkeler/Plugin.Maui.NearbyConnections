namespace Plugin.Maui.NearbyConnections;

internal sealed partial class NearbyConnectionsImplementation
{
#pragma warning disable CA1822, S2325, S1144, S1172
    const string PlatformNotSupportedMessage = "This functionality is not supported in this platform implementation.";

    public bool IsAdvertising => false;
    public bool IsDiscovering => false;

    static void PlatformDispose() { }

    static Task PlatformStartAdvertisingAsync()
        => throw new PlatformNotSupportedException(PlatformNotSupportedMessage);

    static void PlatformStopAdvertising() { }

    static Task PlatformStartDiscoveringAsync()
        => throw new PlatformNotSupportedException(PlatformNotSupportedMessage);

    static void PlatformStopDiscovering() { }

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
