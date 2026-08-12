namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Describes whether nearby connectivity can be started, and what is preventing it if not.
/// </summary>
/// <remarks>
/// <para>
/// A device can fail more than one condition at once — Bluetooth disabled <i>and</i> a required
/// permission denied, for example — so this enumeration is a bit field. Compare the result with
/// <see cref="Ready"/> to test overall readiness, and use <see cref="Enum.HasFlag(Enum)"/> to test
/// for an individual cause.
/// </para>
/// <para>
/// Every applicable flag is set at once, so the result describes everything the user would have to
/// resolve rather than only the first problem encountered.
/// </para>
/// </remarks>
/// <seealso cref="INearby.CheckAvailabilityAsync(CancellationToken)"/>
[Flags]
public enum NearbyAvailability
{
    /// <summary>
    /// Everything required is present, and advertising and discovery can be started.
    /// </summary>
    Ready = 0,

    /// <summary>
    /// One or more required permissions have not been granted.
    /// </summary>
    /// <remarks>
    /// Request the missing permission with the .NET MAUI <c>Permissions</c> API before starting.
    /// This flag only reports that a permission is missing — it does not distinguish a permission
    /// the user has not yet been asked for from one they have permanently denied.
    /// </remarks>
    MissingPermissions = 1 << 0,

    /// <summary>
    /// Bluetooth is supported but currently turned off.
    /// </summary>
    /// <remarks>
    /// Nearby connectivity uses Bluetooth for discovery on both platforms. Prompt the user to turn
    /// it on; an app cannot enable it directly.
    /// </remarks>
    BluetoothDisabled = 1 << 1,

    /// <summary>
    /// Wi-Fi is supported but currently turned off.
    /// </summary>
    /// <remarks>
    /// A connection can often still be established over Bluetooth alone, at substantially lower
    /// throughput, so treat this flag as a warning rather than a hard failure.
    /// </remarks>
    WifiDisabled = 1 << 2,

    /// <summary>
    /// Google Play services is missing, disabled, or requires an update.
    /// </summary>
    /// <remarks>
    /// Android only. Nearby Connections is provided by Google Play services, so this is a hard
    /// failure on Android and is never reported on iOS.
    /// </remarks>
    PlayServicesUnavailable = 1 << 3,

    /// <summary>
    /// The current platform does not support nearby connectivity.
    /// </summary>
    /// <remarks>
    /// Reported on every target other than Android and iOS. Unlike the other flags, this condition
    /// cannot be resolved at run time.
    /// </remarks>
    UnsupportedPlatform = 1 << 4,

    /// <summary>
    /// <see cref="NearbyOptions.ServiceId"/> is not valid for the current platform.
    /// </summary>
    /// <remarks>
    /// iOS only. Starting with an invalid service identifier raises an exception inside Multipeer
    /// Connectivity that terminates the process and cannot be caught, so this condition must be
    /// resolved rather than handled at run time. Options validation normally rejects an invalid
    /// <see cref="NearbyOptions.ServiceId"/> during application startup; this flag exists for
    /// consumers who construct the session outside that pipeline.
    /// </remarks>
    InvalidConfiguration = 1 << 5,
}
