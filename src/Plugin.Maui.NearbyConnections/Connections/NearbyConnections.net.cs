namespace Plugin.Maui.NearbyConnections;

internal sealed partial class NearbyConnectionsImplementation
{
#pragma warning disable CA1822, S2325, S1144, S1172
    const string PlatformNotSupportedMessage = "This functionality is not supported in this platform implementation.";

    // Intentional no-ops: stop/dispose are called from shared cleanup paths (DisposeAsync, finally
    // blocks) where throwing PlatformNotSupportedException would swallow the original exception.
    void PlatformDispose() { }

    void PlatformStopAdvertising() { }

    void PlatformStopDiscovering() { }

    Task PlatformStartAdvertisingAsync(CancellationToken cancellationToken)
        => throw new PlatformNotSupportedException(PlatformNotSupportedMessage);

    Task PlatformStartDiscoveringAsync(CancellationToken cancellationToken)
        => throw new PlatformNotSupportedException(PlatformNotSupportedMessage);

    Task PlatformInitiateConnectAsync(NearbyDevice device, CancellationToken cancellationToken)
        => throw new PlatformNotSupportedException(PlatformNotSupportedMessage);
#pragma warning restore CA1822, S2325, S1144, S1172
}
