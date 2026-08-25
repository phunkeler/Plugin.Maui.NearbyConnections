namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The <c>net10.0</c> adapter: every start throws, so a session on an unsupported platform fails
/// at the operation rather than pretending. Stop, release, and dispose stay no-ops — they run on
/// shared cleanup paths where a <see cref="PlatformNotSupportedException"/> would swallow the
/// caller's original failure. Off-device unit tests use the scripted adapter in the test suite
/// instead; this is the shipping stub.
/// </summary>
sealed class NetThrowingAdapter : IPlatformAdapter
{
    const string PlatformNotSupportedMessage = "Nearby Connections is only supported on Android and iOS. The current platform is not supported.";

    public Task StartAdvertisingAsync(CancellationToken cancellationToken)
        => throw new PlatformNotSupportedException(PlatformNotSupportedMessage);

    public void StopAdvertising()
    {
        // Cleanup path: intentionally inert.
    }

    public Task StartDiscoveryAsync(CancellationToken cancellationToken)
        => throw new PlatformNotSupportedException(PlatformNotSupportedMessage);

    public void StopDiscovering()
    {
        // Cleanup path: intentionally inert.
    }

    public Task InitiateConnectAsync(NearbyDevice device, CancellationToken cancellationToken)
        => throw new PlatformNotSupportedException(PlatformNotSupportedMessage);

    // No-op rather than a throw: it runs on a cleanup path where throwing would replace the
    // caller's real failure with a platform-support error.
    public Task AbandonConnectAsync(NearbyDevice device) => Task.CompletedTask;

    // Reports rather than throws: the whole point of a preflight check is to answer "can I
    // start?" without the caller having to catch anything.
    public Task<NearbyAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(NearbyAvailability.UnsupportedPlatform);
    }

    // No payload ever arrives here, so nothing is staged and there is nothing to sweep.
    public string StagingDirectory => string.Empty;

    public void SweepStaging()
    {
        // Cleanup path: intentionally inert.
    }

    public void ReleaseConnection(string deviceId)
    {
        // Cleanup path: intentionally inert.
    }

    public void Dispose()
    {
        // Cleanup path: intentionally inert.
    }
}
