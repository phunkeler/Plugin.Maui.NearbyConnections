namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// The scripted <see cref="IPlatformAdapter"/>: the fourth implementation of the platform seam,
/// and the one that makes the bridge's own logic — the channel swap, the handshake ledger, the
/// release order — testable on <c>net10.0</c>, where the shipping adapter throws.
/// </summary>
/// <remarks>
/// Every operation defaults to the shipping <c>net10.0</c> behavior (starts throw, availability
/// reports unsupported), so a test that scripts nothing observes the stub. A test scripts an
/// operation by assigning its delegate.
/// </remarks>
sealed class ScriptedAdapter : IPlatformAdapter
{
    const string NotSupported = "Not scripted: the default mirrors the net10.0 throwing adapter.";

    /// <summary>What <see cref="StartAdvertisingAsync"/> runs. Defaults to a throw.</summary>
    public Func<CancellationToken, Task> OnStartAdvertising { get; set; }
        = static _ => throw new PlatformNotSupportedException(NotSupported);

    /// <summary>What <see cref="StartDiscoveryAsync"/> runs. Defaults to a throw.</summary>
    public Func<CancellationToken, Task> OnStartDiscovery { get; set; }
        = static _ => throw new PlatformNotSupportedException(NotSupported);

    /// <summary>What <see cref="InitiateConnectAsync"/> runs. Defaults to a throw.</summary>
    public Func<NearbyDevice, CancellationToken, Task> OnInitiateConnect { get; set; }
        = static (_, _) => throw new PlatformNotSupportedException(NotSupported);

    /// <summary>Gets the devices whose handshakes were abandoned, in order.</summary>
    public List<NearbyDevice> Abandoned { get; } = [];

    /// <summary>Gets the device ids whose platform bookkeeping was released, in order.</summary>
    public List<string> Released { get; } = [];

    /// <summary>Gets how many times the adapter was disposed.</summary>
    public int DisposeCount { get; private set; }

    public Task StartAdvertisingAsync(CancellationToken cancellationToken)
        => OnStartAdvertising(cancellationToken);

    public void StopAdvertising()
    {
        // Scripted: nothing to release.
    }

    public Task StartDiscoveryAsync(CancellationToken cancellationToken)
        => OnStartDiscovery(cancellationToken);

    public void StopDiscovering()
    {
        // Scripted: nothing to release.
    }

    public Task InitiateConnectAsync(NearbyDevice device, CancellationToken cancellationToken)
        => OnInitiateConnect(device, cancellationToken);

    public Task AbandonConnectAsync(NearbyDevice device)
    {
        Abandoned.Add(device);
        return Task.CompletedTask;
    }

    public Task<NearbyAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(NearbyAvailability.UnsupportedPlatform);
    }

    public string StagingDirectory => string.Empty;

    public void SweepStaging()
    {
        // Scripted: nothing staged.
    }

    public void ReleaseConnection(string deviceId) => Released.Add(deviceId);

    public void Dispose() => DisposeCount++;
}
