namespace Plugin.Maui.NearbyConnections;

sealed partial class PlatformNearby
{
    /// <summary>
    /// The Android adapter, typed — for the device tests, which drive its SDK-typed callback
    /// entry points directly.
    /// </summary>
    internal AndroidAdapter AndroidAdapter => (AndroidAdapter)_adapter!;

    partial void PlatformCreateAdapter() => _adapter = new AndroidAdapter(this);

    Task PlatformStartAdvertisingAsync(CancellationToken cancellationToken)
        => _adapter!.StartAdvertisingAsync(cancellationToken);

    void PlatformStopAdvertising() => _adapter!.StopAdvertising();

    Task PlatformStartDiscoveryAsync(CancellationToken cancellationToken)
        => _adapter!.StartDiscoveryAsync(cancellationToken);

    void PlatformStopDiscovering() => _adapter!.StopDiscovering();

    Task PlatformInitiateConnectAsync(NearbyDevice device, CancellationToken cancellationToken)
        => _adapter!.InitiateConnectAsync(device, cancellationToken);

    Task PlatformAbandonConnectAsync(NearbyDevice device) => _adapter!.AbandonConnectAsync(device);

    Task<NearbyAvailability> PlatformCheckAvailabilityAsync(CancellationToken cancellationToken)
        => _adapter!.CheckAvailabilityAsync(cancellationToken);

    partial void PlatformReleaseConnection(string deviceId) => _adapter!.ReleaseConnection(deviceId);

    internal static partial string StagingDirectory
        => Path.Combine(FileSystem.CacheDirectory, StagingDirectoryName);

    void PlatformSweepStaging() => _adapter!.SweepStaging();

    void PlatformDispose() => _adapter!.Dispose();
}
