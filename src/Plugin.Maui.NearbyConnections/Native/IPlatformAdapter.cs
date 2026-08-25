namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// The outbound platform seam: the operations the bridge asks a platform SDK to perform. One
/// implementation per backend — Android (GMS Nearby Connections), iOS (MultipeerConnectivity), a
/// throwing <c>net10.0</c> adapter, and a scripted test adapter in the unit suite. The members
/// mirror the <c>Platform*</c> partial-method list this contract grew out of, which had been
/// stable across the codebase's whole life (<c>docs/ARCHITECTURE.md</c> section 4, decision D5).
/// </summary>
/// <remarks>
/// <para>
/// The seam is deliberately asymmetric. This outbound direction is the interface: it is what a
/// new backend implements, and the compiler checks it. The inbound direction — SDK callbacks
/// into the bridge — stays concrete: adapters call the bridge's internal <c>On*</c>/<c>Write*</c>
/// methods directly, which is the surface the device tests drive.
/// </para>
/// <para>
/// Connection-scoped operations are not here: an established link is represented by the
/// <see cref="IPlatformConnection"/> the adapter produces, which captures its native handle once.
/// </para>
/// </remarks>
interface IPlatformAdapter : IDisposable
{
    /// <summary>Starts advertising this device. Throws the typed start failure.</summary>
    /// <param name="cancellationToken">A token to abandon the start.</param>
    Task StartAdvertisingAsync(CancellationToken cancellationToken);

    /// <summary>Stops advertising and releases the advertise-side SDK objects. Never throws.</summary>
    void StopAdvertising();

    /// <summary>Starts discovery. Throws the typed start failure.</summary>
    /// <param name="cancellationToken">A token to abandon the start.</param>
    Task StartDiscoveryAsync(CancellationToken cancellationToken);

    /// <summary>Stops discovery and releases the discovery-side SDK objects. Never throws.</summary>
    void StopDiscovering();

    /// <summary>
    /// Asks the platform to open a connection to <paramref name="device"/>. Completes when the
    /// request is sent — the terminal callback, not this task, resolves the handshake ledger.
    /// </summary>
    /// <param name="device">The device to connect to.</param>
    /// <param name="cancellationToken">A token to abandon the send.</param>
    Task InitiateConnectAsync(NearbyDevice device, CancellationToken cancellationToken);

    /// <summary>
    /// Abandons a handshake that will not complete, releasing whatever the platform holds for it.
    /// Never throws — failures log.
    /// </summary>
    /// <param name="device">The device whose handshake to abandon.</param>
    Task AbandonConnectAsync(NearbyDevice device);

    /// <summary>Reports whether the platform can start right now. Never prompts, never mutates.</summary>
    /// <param name="cancellationToken">A token to cancel the check.</param>
    Task<NearbyAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken);

    /// <summary>The directory inbound files are staged into on this platform.</summary>
    string StagingDirectory { get; }

    /// <summary>Deletes every file left in the staging directory. Called once, at disposal.</summary>
    void SweepStaging();

    /// <summary>
    /// Drops the platform-side bookkeeping for one released connection, after the bridge drained
    /// the work that read its handles (contract C7).
    /// </summary>
    /// <param name="deviceId">The device whose bookkeeping to drop.</param>
    void ReleaseConnection(string deviceId);
}
