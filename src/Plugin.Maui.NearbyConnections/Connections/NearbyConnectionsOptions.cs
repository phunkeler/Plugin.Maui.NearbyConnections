namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// One-time startup configuration for Nearby Connections. Set values in the
/// <c>UseNearbyConnections</c>/<c>AddNearbyConnections</c> configure delegate; the plugin reads
/// the resolved instance once at construction, so mutating properties after startup is
/// unsupported and has no defined effect.
/// </summary>
public sealed partial class NearbyConnectionsOptions
{
    /// <summary>
    /// Gets the name to display when advertising/discovering.
    /// Defaults to <see cref="DeviceInfo.Name"/>.
    /// </summary>
    public string DisplayName { get; set; } = GetDefaultDisplayName();

    /// <summary>
    /// Gets the service identifier used to discover and connect with nearby devices.
    /// On Android, defaults to <see cref="AppInfo.Name"/>. On iOS, this property has no default
    /// and <b>must</b> be set explicitly before calling <c>AdvertiseAsync</c> or <c>DiscoverAsync</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On Android, this is used as the <c>serviceId</c> when advertising/discovery Google Play Service's Nearby Connections API
    /// (<see href="https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/package-summary">developers.google.com</see>).
    /// </para>
    /// <para>
    /// On iOS, this is passed directly as <c>MCNearbyServiceAdvertiser</c>/
    /// <c>MCNearbyServiceBrowser</c>'s <c>serviceType</c>, which Apple requires to be a bare
    /// string 1-15 characters long identifying the network protocol (for example
    /// <c>"xamarin-txtchat"</c>) — this is <b>not</b> the same as the Bonjour <c>_name._tcp</c>
    /// service type format used in the application's <c>Info.plist</c> under
    /// <c>NSBonjourServices</c>
    /// (<see href="https://developer.apple.com/documentation/BundleResources/Information-Property-List/NSBonjourServices">developer.apple.com</see>);
    /// that longer form must still be declared in <c>Info.plist</c>, but this property's value
    /// itself must be the short <c>serviceType</c> form. There is no meaningful default on iOS;
    /// app startup will fail if this property is not set.
    /// </para>
    /// </remarks>
    public string ServiceId { get; set; } = GetDefaultServiceId();

    /// <summary>
    /// Gets the directory where received files are saved after transfer.
    /// The default differs per platform: on Android, <see cref="FileSystem.CacheDirectory"/>
    /// (which the OS may purge to reclaim space); on iOS, <see cref="FileSystem.AppDataDirectory"/>
    /// (persistent). Set this explicitly (or move files after receipt) if received files must
    /// persist on Android.
    /// </summary>
    public string ReceivedFilesDirectory { get; set; } = GetDefaultReceivedFilesDirectory();

    private static partial string GetDefaultDisplayName();
    private static partial string GetDefaultServiceId();
    private static partial string GetDefaultReceivedFilesDirectory();

    /// <summary>
    /// Gets or sets how long to wait for a remote device to answer a connection request before the
    /// attempt is abandoned. Defaults to 30 seconds. Set to <see cref="Timeout.InfiniteTimeSpan"/>
    /// to wait indefinitely.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Applies to both platforms, but is enforced differently: iOS has a native invitation timeout,
    /// while on Android the plugin owns a timer because Google's Nearby Connections imposes no
    /// timeout of its own. Without it, connecting to a device that never answers — or that walks
    /// out of range mid-handshake — would wait forever.
    /// </para>
    /// <para>
    /// On expiry <c>ConnectAsync</c> throws <see cref="NearbyConnectionTimeoutException"/>.
    /// </para>
    /// </remarks>
    public TimeSpan InvitationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the maximum time to wait without receiving a transfer progress update
    /// before considering a data transfer stalled and aborting it.
    /// Defaults to 10 seconds. Set to <see cref="Timeout.InfiniteTimeSpan"/> to disable.
    /// </summary>
    public TimeSpan TransferInactivityTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets a value indicating whether reader continuations on internal event/payload channels
    /// may run synchronously on the writer's thread instead of being scheduled to the thread pool.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Channel writes originate from SDK-owned native callback threads (see the platform callback
    /// remarks on <see cref="INearbyConnections"/>). Setting this to <see langword="true"/> means your
    /// <c>await foreach</c> body may execute directly on that native thread, avoiding a thread-pool
    /// hop — but a slow consumer body will stall the platform SDK's own callback dispatch. Only
    /// enable this if your <c>AdvertiseAsync</c>/<c>DiscoverAsync</c>/<c>ReceiveAsync</c> consumer
    /// bodies are trivially fast (e.g. forwarding to another channel).
    /// </remarks>
    public bool AllowSynchronousContinuations { get; set; }
}