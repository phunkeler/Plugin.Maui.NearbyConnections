namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Configures nearby connectivity: display identity, service discovery, timeouts, and
/// platform-specific behavior.
/// </summary>
/// <remarks>
/// Configure this type in the delegate passed to
/// <see cref="MauiAppBuilderExtensions"/>.<c>UseNearby</c> or
/// <see cref="ServiceCollectionExtensions"/>.<c>AddNearby</c>. The library validates the options
/// and captures an immutable copy before that call returns — changing a property afterward has no
/// effect on the session.
/// </remarks>
public sealed partial class NearbyOptions
{
    /// <summary>
    /// Gets the Android-specific settings.
    /// </summary>
    /// <value>The Android options. Never <see langword="null"/>.</value>
    /// <remarks>
    /// Exposed on every target framework so shared code compiles without <c>#if ANDROID</c>. On
    /// other platforms nothing reads these settings and they have no effect. The nesting under this
    /// property names that at the call site. The property itself is get-only, so the returned
    /// instance can be configured in place but never replaced or shared with another
    /// <see cref="NearbyOptions"/> instance.
    /// </remarks>
    public NearbyAndroidOptions Android { get; } = new();

    /// <summary>
    /// Gets the Apple-platform-specific settings.
    /// </summary>
    /// <value>The Apple options. Never <see langword="null"/>.</value>
    /// <remarks>
    /// Exposed on every target framework so shared code compiles without <c>#if IOS</c>. On other
    /// platforms nothing reads these settings and they have no effect. The nesting under this
    /// property names that at the call site. The property itself is get-only, so the returned
    /// instance can be configured in place but never replaced or shared with another
    /// <see cref="NearbyOptions"/> instance.
    /// </remarks>
    public NearbyAppleOptions Apple { get; } = new();

    /// <summary>
    /// Gets or sets the name shown to nearby devices when advertising or discovering.
    /// </summary>
    /// <value>The display name for this device. The default is <see cref="DeviceInfo.Name"/>.</value>
    public string DisplayName { get; set; } = GetDefaultDisplayName();

    /// <summary>
    /// Gets or sets the service identifier that devices use to find one another.
    /// </summary>
    /// <value>
    /// The service identifier. On Android, the default is <see cref="AppInfo.Name"/>. On iOS, this
    /// property has no usable default and must be set explicitly.
    /// </value>
    /// <remarks>
    /// <para>
    /// Only devices configured with the same service identifier discover one another.
    /// </para>
    /// <para>
    /// On Android, this value is passed as the service identifier to the Nearby Connections API.
    /// See <see href="https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/package-summary">Nearby Connections API</see>.
    /// </para>
    /// <para>
    /// On iOS, this value is passed as the service type to Multipeer Connectivity, which requires a
    /// string of 1 to 15 characters identifying the network protocol — for example,
    /// <c>"nearbychat"</c>. This is <b>not</b> the same as the Bonjour <c>_name._tcp</c> service
    /// type declared in the application's <c>Info.plist</c> file under <c>NSBonjourServices</c>.
    /// Both are required, but they take different forms: declare the longer form in
    /// <c>Info.plist</c>, and assign the short form here. See
    /// <see href="https://developer.apple.com/documentation/BundleResources/Information-Property-List/NSBonjourServices">NSBonjourServices</see>.
    /// </para>
    /// <para>
    /// On iOS, application startup fails with a descriptive error if this property is not set, or
    /// if its length is outside the supported range — an invalid service type otherwise causes an
    /// unrecoverable native failure.
    /// </para>
    /// </remarks>
    public string ServiceId { get; set; } = GetDefaultServiceId();

    /// <summary>
    /// Gets or sets how often discovery is restarted to drop devices that have gone away without the
    /// platform reporting it.
    /// </summary>
    /// <value>
    /// The interval between discovery passes, or <see langword="null"/> to never restart. The
    /// default is 30 seconds.
    /// </value>
    /// <remarks>
    /// <para>
    /// Neither platform reliably reports every departure — a device that is switched off or carried
    /// out of range can simply stop being seen, leaving a row in <see cref="INearby.Devices"/> that
    /// can never be connected to. Both platforms report discovery on an edge: once when a device
    /// appears, rather than continuously. Elapsed silence therefore says nothing on its own.
    /// Restarting discovery is the only way to re-establish what is actually in range, because a
    /// completed pass reports everything.
    /// </para>
    /// <para>
    /// After each restart, devices the new pass does not re-report are removed. A device that is
    /// connected, or mid-handshake, is never removed no matter how long it has been quiet.
    /// </para>
    /// <para>
    /// Lower values detect departures sooner at the cost of more frequent radio work. Set this to
    /// <see langword="null"/> if the application drives
    /// <see cref="INearby.StopDiscoveryAsync(CancellationToken)"/> and
    /// <see cref="INearby.StartDiscoveryAsync(CancellationToken)"/> itself, or if stale entries are
    /// acceptable.
    /// </para>
    /// </remarks>
    public TimeSpan? DiscoveryRefreshInterval { get; set; } = TimeSpan.FromSeconds(30);

    private static partial string GetDefaultDisplayName();
    private static partial string GetDefaultServiceId();

    /// <summary>
    /// Gets or sets how long <see cref="INearby.ConnectAsync(NearbyDevice, CancellationToken)"/>
    /// waits for a connection before the attempt is abandoned.
    /// </summary>
    /// <value>
    /// The interval to wait, measured from the call. The default is 30 seconds. Set this to
    /// <see cref="Timeout.InfiniteTimeSpan"/> to wait indefinitely.
    /// </value>
    /// <remarks>
    /// <para>
    /// This window covers the request reaching the remote device, <b>the remote user deciding</b>,
    /// and the handshake completing. The human step is usually the largest part, which is why the
    /// default is generous and why this is a separate setting from
    /// <see cref="AcceptTimeout"/>.
    /// </para>
    /// <para>
    /// <see cref="NearbyConnectionTimeoutException"/> is thrown when the interval elapses, and the
    /// device returns to <see cref="NearbyDeviceStatus.Visible"/>.
    /// </para>
    /// <para>
    /// The library enforces this itself on both platforms. Google Nearby Connections has no
    /// equivalent — <c>requestConnection</c> completes when the request is sent, and nothing
    /// guarantees a callback follows. MultipeerConnectivity has a native invitation timeout, but it
    /// bounds the inviting side only. Without this option, a device that moves out of range
    /// mid-handshake would leave the caller waiting without end.
    /// </para>
    /// </remarks>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how long <see cref="INearby.AcceptAsync(NearbyDevice, CancellationToken)"/>
    /// waits for a connection before the attempt is abandoned.
    /// </summary>
    /// <value>
    /// The interval to wait, measured from the call. The default is 15 seconds. Set this to
    /// <see cref="Timeout.InfiniteTimeSpan"/> to wait indefinitely.
    /// </value>
    /// <remarks>
    /// <para>
    /// Shorter than <see cref="ConnectTimeout"/> by default: the decision to accept is already made,
    /// so only the handshake remains. A device that leaves range mid-handshake reports no terminal
    /// result on either platform, so this deadline is the only thing that ends the attempt.
    /// <see cref="NearbyConnectionTimeoutException"/> is thrown when the interval elapses, and the
    /// device returns to <see cref="NearbyDeviceStatus.Visible"/>.
    /// </para>
    /// <para>
    /// This bounds the accept, not the offer. To bound how long an unanswered inbound request stays
    /// outstanding before the library withdraws it, set <see cref="InboundRequestTimeout"/>.
    /// </para>
    /// </remarks>
    public TimeSpan AcceptTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets how long an unanswered inbound connection request stays outstanding before the
    /// library rejects it on the application's behalf.
    /// </summary>
    /// <value>
    /// The interval to leave a request outstanding. The default is 30 seconds. Set this to
    /// <see cref="Timeout.InfiniteTimeSpan"/> to leave requests outstanding until the application
    /// answers them or the session stops.
    /// </value>
    /// <remarks>
    /// <para>
    /// Alone among the three timeouts, this one bounds state rather than an operation. Nothing
    /// throws when it elapses, because no caller is waiting: the library rejects the request and the
    /// device returns to <see cref="NearbyDeviceStatus.Visible"/>. A later
    /// <see cref="INearby.AcceptAsync(NearbyDevice, CancellationToken)"/> for that device throws
    /// <see cref="InvalidOperationException"/>, as it does for any request no longer outstanding.
    /// </para>
    /// <para>
    /// The remote device's own timeout is not observable: neither platform transmits it. So this
    /// value states when <em>this</em> device withdraws the offer, and never predicts when the
    /// asking device gives up. Without an expiry the request would outlive the remote device's own
    /// attempt — on iOS, MultipeerConnectivity expires the invitation on the inviting side, so a
    /// request left outstanding past that point can never be accepted successfully.
    /// </para>
    /// <para>
    /// Read <see cref="NearbyDevice.RequestExpiresAt"/> to show a countdown.
    /// </para>
    /// </remarks>
    public TimeSpan InboundRequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how long a file transfer may report no progress before it is considered stalled
    /// and is abandoned.
    /// </summary>
    /// <value>
    /// The interval to wait between progress updates. The default is 10 seconds. Set this to
    /// <see cref="Timeout.InfiniteTimeSpan"/> to disable the check.
    /// </value>
    /// <remarks>
    /// <para>
    /// When this interval elapses, the pending <c>SendAsync</c> call throws
    /// <see cref="NearbyTransferTimeoutException"/>.
    /// </para>
    /// <para>
    /// This value covers outbound transfers. An inbound file is copied out of the platform's own
    /// storage before it reaches the application, and disposal waits for a copy that is still
    /// running. That wait has a fixed internal bound of a few seconds, so disposing a session
    /// during a large inbound transfer can take that much longer to return.
    /// </para>
    /// </remarks>
    public TimeSpan TransferInactivityTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets a value that indicates whether inbound connection requests are accepted
    /// automatically, without the application calling
    /// <see cref="INearby.AcceptAsync(NearbyDevice, CancellationToken)"/>.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to accept every inbound request as it arrives; otherwise,
    /// <see langword="false"/>. The default is <see langword="false"/>.
    /// </value>
    /// <remarks>
    /// <para>
    /// When this is <see langword="false"/>, an inbound request moves the device to
    /// <see cref="NearbyDeviceStatus.RequestReceived"/>, reported through
    /// <see cref="INearbyDevices.Changes"/>, and the application must answer it with
    /// <see cref="INearby.AcceptAsync(NearbyDevice, CancellationToken)"/> or
    /// <see cref="INearby.RejectAsync(NearbyDevice, CancellationToken)"/> before
    /// <see cref="InboundRequestTimeout"/> elapses. Once it does, the library rejects the request
    /// and the device returns to <see cref="NearbyDeviceStatus.Visible"/>. Raise
    /// <see cref="InboundRequestTimeout"/> to allow a longer answering window —
    /// <see cref="ConnectTimeout"/> bounds the remote initiator's own
    /// <see cref="INearby.ConnectAsync(NearbyDevice, CancellationToken)"/> call and has no effect
    /// here.
    /// </para>
    /// <para>
    /// When it is <see langword="true"/>, the session answers on the application's behalf, so
    /// <see cref="NearbyDeviceStatus.RequestReceived"/> is never observed — the device moves from
    /// <see cref="NearbyDeviceStatus.Visible"/> through
    /// <see cref="NearbyDeviceStatus.Connecting"/> to <see cref="NearbyDeviceStatus.Connected"/>
    /// with that state skipped. Calling
    /// <see cref="INearby.AcceptAsync(NearbyDevice, CancellationToken)"/> at that point throws
    /// <see cref="InvalidOperationException"/>, because no request is outstanding.
    /// </para>
    /// <para>
    /// <b>This accepts every request from any device that knows the service identifier.</b> Neither
    /// platform authenticates the remote device, so enable this only where an unsolicited connection
    /// is acceptable — a kiosk, a paired-appliance scenario, or a trusted network. Prompting the user
    /// is the safer default, which is why it is the default here.
    /// </para>
    /// </remarks>
    public bool AutoAcceptConnectionRequests { get; set; }

    /// <summary>
    /// Returns a copy of this instance, including the platform scopes. The session holds the copy,
    /// so the caller's instance stays free to mutate without effect (contract C5: the configured
    /// options have one owner — the snapshot).
    /// </summary>
    internal NearbyOptions Snapshot()
    {
        var copy = new NearbyOptions
        {
            DisplayName = DisplayName,
            ServiceId = ServiceId,
            DiscoveryRefreshInterval = DiscoveryRefreshInterval,
            ConnectTimeout = ConnectTimeout,
            AcceptTimeout = AcceptTimeout,
            InboundRequestTimeout = InboundRequestTimeout,
            TransferInactivityTimeout = TransferInactivityTimeout,
            AutoAcceptConnectionRequests = AutoAcceptConnectionRequests,
        };

        copy.Android.Topology = Android.Topology;
        copy.Android.UseLowPower = Android.UseLowPower;
        copy.Android.ConnectionType = Android.ConnectionType;
        copy.Apple.EncryptionPreference = Apple.EncryptionPreference;
        copy.Apple.StartFailureGraceWindow = Apple.StartFailureGraceWindow;

        return copy;
    }
}