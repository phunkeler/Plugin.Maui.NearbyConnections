namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Describes whether nearby connectivity can be started, and what is preventing it if not.
/// </summary>
/// <remarks>
/// <para>
/// More than one condition can apply at once — a device may have Bluetooth disabled
/// <i>and</i> the required permissions denied — so this enumeration is a bit field. Test for
/// readiness by comparing with <see cref="Ready"/>, and test individual causes with
/// <see cref="Enum.HasFlag(Enum)"/>.
/// </para>
/// <para>
/// Conditions are reported independently of one another, so the result describes everything the
/// user would have to resolve rather than only the first problem encountered.
/// </para>
/// </remarks>
/// <seealso cref="INearbyConnections.CheckAvailabilityAsync(CancellationToken)"/>
[Flags]
public enum NearbyAvailability
{
    /// <summary>
    /// Everything required is present. Advertising and discovery can be started.
    /// </summary>
    Ready = 0,

    /// <summary>
    /// One or more required permissions have not been granted.
    /// </summary>
    /// <remarks>
    /// Request them with the .NET MAUI <c>Permissions</c> API before starting. This flag reports
    /// only that a permission is missing; it does not distinguish a permission the user has not
    /// been asked for from one they have permanently denied.
    /// </remarks>
    MissingPermissions = 1 << 0,

    /// <summary>
    /// Bluetooth is supported but currently turned off.
    /// </summary>
    /// <remarks>
    /// Nearby connectivity uses Bluetooth for discovery on both platforms. Prompt the user to
    /// enable it; the operating system does not allow an app to enable it directly.
    /// </remarks>
    BluetoothDisabled = 1 << 1,

    /// <summary>
    /// Wi-Fi is supported but currently turned off.
    /// </summary>
    /// <remarks>
    /// Connections can often still be established over Bluetooth alone, at substantially lower
    /// throughput. Treat this as a warning rather than a hard failure.
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
    /// Reported on every target other than Android and iOS. This condition cannot be resolved at
    /// run time.
    /// </remarks>
    UnsupportedPlatform = 1 << 4,

    /// <summary>
    /// <see cref="NearbyConnectionsOptions.ServiceId"/> is not valid for the current platform.
    /// </summary>
    /// <remarks>
    /// iOS only. Starting with an invalid service identifier raises an exception inside Multipeer
    /// Connectivity that terminates the process and cannot be caught, so this condition must be
    /// resolved rather than handled. Options validation normally rejects it during application
    /// startup; this flag exists for consumers who construct the session outside that pipeline.
    /// </remarks>
    InvalidConfiguration = 1 << 5,
}
