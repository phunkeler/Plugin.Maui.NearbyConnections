namespace Plugin.Maui.NearbyConnections;

sealed partial class PlatformNearby
{
#pragma warning disable CA1822, S2325, S1144, S1172
    const string PlatformNotSupportedMessage = "Nearby Connections is only supported on Android and iOS. The current platform is not supported.";

    // Intentional no-ops: stop/dispose are called from shared cleanup paths (DisposeAsync, finally
    // blocks) where throwing PlatformNotSupportedException would swallow the original exception.
    // This target creates no adapter, so the null-conditional call compiles the field's use away.
    void PlatformDispose() => _adapter?.Dispose();

    // No payload ever arrives here, so nothing is staged and there is nothing to sweep.
    internal static partial string StagingDirectory => string.Empty;

    void PlatformSweepStaging() { }

    void PlatformStopAdvertising() { }

    void PlatformStopDiscovering() { }

    Task PlatformStartAdvertisingAsync(CancellationToken cancellationToken)
        => throw new PlatformNotSupportedException(PlatformNotSupportedMessage);

    Task PlatformStartDiscoveryAsync(CancellationToken cancellationToken)
        => throw new PlatformNotSupportedException(PlatformNotSupportedMessage);

    Task PlatformInitiateConnectAsync(NearbyDevice device, CancellationToken cancellationToken)
        => throw new PlatformNotSupportedException(PlatformNotSupportedMessage);

    // No-op for the same reason as the stop/dispose members above: it runs on a cleanup path where
    // throwing would replace the caller's real failure with a platform-support error.
    Task PlatformAbandonConnectAsync(NearbyDevice device) => Task.CompletedTask;

    // Reports rather than throws: the whole point of a preflight check is to answer "can I start?"
    // without the caller having to catch anything.
    static Task<NearbyAvailability> PlatformCheckAvailabilityAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(NearbyAvailability.UnsupportedPlatform);
    }
#pragma warning restore CA1822, S2325, S1144, S1172
}