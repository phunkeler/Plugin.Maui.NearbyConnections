namespace Plugin.Maui.NearbyConnections.Advertise;

/// <summary>
/// Options for configuring advertising behavior.
/// </summary>
public class AdvertisingOptions
{
    /// <summary>
    /// Gets or sets a user-friendly name for this device that appears on nearby devices upon discovery.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Android:
    /// <a href="https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/ConnectionsClient#public-abstract-taskvoid-startadvertising-string-name,-string-serviceid,-connectionlifecyclecallback-connectionlifecyclecallback,-advertisingoptions-options">
    ///     name:
    /// </a>
    /// A human readable name for this endpoint, to appear on the remote device. Defined by client/application.
    /// </para>
    /// <para>
    /// iOS:
    /// <a href="https://developer.apple.com/documentation/multipeerconnectivity/mcpeerid/displayname">
    ///     displayName:
    /// </a>
    /// The display name for this peer.
    /// </para>
    /// </remarks>
    public string DisplayName { get; set; } = DeviceInfo.Name;

    /// <summary>
    /// Gets or sets the name of the service to advertise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Android:
    /// <a href="https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/ConnectionsClient#public-abstract-taskvoid-startadvertising-string-name,-string-serviceid,-connectionlifecyclecallback-connectionlifecyclecallback,-advertisingoptions-options">
    ///     serviceId:
    /// </a>
    /// An identifier to advertise your app to other endpoints. This can be an arbitrary string, so long as it uniquely identifies your service. A good default is to use your app's package name.
    /// </para>
    /// <para>
    /// iOS:
    /// <a href="https://developer.apple.com/documentation/multipeerconnectivity/mcnearbyserviceadvertiser/servicetype">
    ///     serviceType:
    /// </a>
    /// A short text string that describes the app’s networking protocol, in the same format as a Bonjour service type (<i>without the transport protocol</i>).
    /// </para>
    /// </remarks>
    public string ServiceName { get; set; } = "Plugin.Maui.NearbyConnections";

    /// <summary>
    /// Gets or sets additional information to include in the advertisement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Android:
    /// <a href="https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/ConnectionsClient#public-abstract-taskvoid-startadvertising-byte[]-endpointinfo,-string-serviceid,-connectionlifecyclecallback-connectionlifecyclecallback,-advertisingoptions-options">
    ///     endpointInfo:
    /// </a>
    /// Identifing information about this endpoint (eg. name, device type), to appear on the remote device.
    /// </para>
    /// <para>
    /// iOS:
    /// <a href="https://developer.apple.com/documentation/multipeerconnectivity/mcnearbyserviceadvertiser/servicetype">
    ///     info:
    /// </a>
    /// A dictionary of key-value pairs that are made available to browsers. Each key/value pair should not exceed 255 Bytes (UTF-8 encoded).
    /// The total size of the collection should not exceed 400 bytes.
    /// </para>
    /// </remarks>
    public IDictionary<string, string> AdvertisingInfo { get; set; } = new Dictionary<string, string>();
}