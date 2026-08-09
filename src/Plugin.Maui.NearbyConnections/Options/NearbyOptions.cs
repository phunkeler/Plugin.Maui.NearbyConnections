namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Provides configuration options for nearby connectivity.
/// </summary>
/// <remarks>
/// Set these options in the delegate passed to
/// <see cref="MauiAppBuilderExtensions"/>.<c>UseNearby</c> or
/// <see cref="ServiceCollectionExtensions"/>.<c>AddNearby</c>. The library reads the
/// resolved instance once, when the session is created; changing a property after application
/// startup has no defined effect.
/// </remarks>
public sealed partial class NearbyOptions
{
    /// <summary>
    /// Gets the Android-specific settings.
    /// </summary>
    /// <value>
    /// The Android options. Never <see langword="null"/>.
    /// </value>
    /// <remarks>
    /// Present on every target framework so shared code compiles without <c>#if ANDROID</c>. On
    /// other platforms these settings are read by nothing and have no effect — the nesting names
    /// the platform at the call site so that is visible where the value is set.
    /// </remarks>
    public NearbyAndroidOptions Android { get; } = new();

    /// <summary>
    /// Gets the Apple-platform-specific settings.
    /// </summary>
    /// <value>
    /// The Apple options. Never <see langword="null"/>.
    /// </value>
    /// <remarks>
    /// Present on every target framework so shared code compiles without <c>#if IOS</c>. On other
    /// platforms these settings are read by nothing and have no effect — the nesting names the
    /// platform at the call site so that is visible where the value is set.
    /// </remarks>
    public NearbyAppleOptions Apple { get; } = new();

    /// <summary>
    /// Gets or sets the name shown to nearby devices when advertising or discovering.
    /// </summary>
    /// <value>
    /// The display name for this device. The default is <see cref="DeviceInfo.Name"/>.
    /// </value>
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
    /// if its length is outside the supported range, because an invalid service type causes an
    /// unrecoverable native failure.
    /// </para>
    /// </remarks>
    public string ServiceId { get; set; } = GetDefaultServiceId();

    /// <summary>
    /// Gets or sets the directory in which received files are saved.
    /// </summary>
    /// <value>
    /// The full path to the destination directory. On Android, the default is
    /// <see cref="FileSystem.CacheDirectory"/>; on iOS, it is
    /// <see cref="FileSystem.AppDataDirectory"/>.
    /// </value>
    /// <remarks>
    /// The Android default is a cache directory, which the operating system may purge to reclaim
    /// space. Set this property to a persistent location, or move received files after they
    /// arrive, if they must survive.
    /// </remarks>
    public string ReceivedFilesDirectory { get; set; } = GetDefaultReceivedFilesDirectory();

    private static partial string GetDefaultDisplayName();
    private static partial string GetDefaultServiceId();
    private static partial string GetDefaultReceivedFilesDirectory();

    /// <summary>
    /// Gets or sets how long to wait for a remote device to answer a connection request before the
    /// attempt is abandoned.
    /// </summary>
    /// <value>
    /// The interval to wait for a response. The default is 30 seconds. Set this to
    /// <see cref="Timeout.InfiniteTimeSpan"/> to wait indefinitely.
    /// </value>
    /// <remarks>
    /// <para>
    /// This timeout applies on both platforms, but is enforced differently. iOS has a native
    /// invitation timeout, whereas on Android the library maintains its own timer, because the
    /// Nearby Connections API imposes no timeout. Without it, connecting to a device that never
    /// answers, or that moves out of range during the handshake, would wait indefinitely.
    /// </para>
    /// <para>
    /// When this interval elapses,
    /// <see cref="INearby.ConnectAsync(NearbyDevice, CancellationToken)"/> throws
    /// <see cref="NearbyConnectionTimeoutException"/>.
    /// </para>
    /// </remarks>
    public TimeSpan InvitationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how long a file transfer may report no progress before it is considered stalled
    /// and is abandoned.
    /// </summary>
    /// <value>
    /// The interval to wait between progress updates. The default is 10 seconds. Set this to
    /// <see cref="Timeout.InfiniteTimeSpan"/> to disable the check.
    /// </value>
    /// <remarks>
    /// When this interval elapses, the pending <c>SendAsync</c> call throws
    /// <see cref="NearbyTransferTimeoutException"/>.
    /// </remarks>
    public TimeSpan TransferInactivityTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets a value indicating whether payload and event delivery may continue synchronously
    /// on the platform callback thread instead of being scheduled to the thread pool.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to allow synchronous continuations; otherwise,
    /// <see langword="false"/>. The default is <see langword="false"/>.
    /// </value>
    /// <remarks>
    /// Payloads and device events are written from background threads owned by the platform SDK.
    /// Setting this property to <see langword="true"/> allows the body of a consuming
    /// <c>await foreach</c> loop to run directly on that thread, avoiding a thread-pool transition.
    /// A slow loop body then stalls the platform SDK's own callback dispatch, so enable this only
    /// when consuming loops complete very quickly.
    /// </remarks>
    public bool AllowSynchronousContinuations { get; set; }
}