using Android.App;

namespace Plugin.Maui.NearbyConnections.Advertise;

/// <summary>
/// Options that describe "how" to advertise.
/// </summary>
/// <remarks>
/// These options can be set at; `MauiProgram.cs` (_simple_) or per `StartAdvertisingAsync` call (_advanced_).
/// </remarks>
public class AdvertiseOptions
{
    /// <summary>
    /// Gets or sets the name displayed to nearby devices during discovery.
    /// </summary>
    /// <remarks>
    /// Reference: <see href="https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/ConnectionsClient#public-abstract-taskvoid-startadvertising-byte[]-endpointinfo,-string-serviceid,-connectionlifecyclecallback-connectionlifecyclecallback,-advertisingoptions-options">Android</see>,
    /// <see href="https://developer.apple.com/documentation/multipeerconnectivity/mcpeerid/displayname">iOS</see>
    /// </remarks>
    public string DisplayName { get; set; } = DeviceInfo.Current.Name;

    /// <summary>
    /// Gets or sets the name of the service being advertised.
    /// </summary>
    /// <remarks>
    /// Reference: <see href="https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/ConnectionsClient#public-abstract-taskvoid-startadvertising-byte[]-endpointinfo,-string-serviceid,-connectionlifecyclecallback-connectionlifecyclecallback,-advertisingoptions-options">Android</see>,
    /// <see href="https://developer.apple.com/documentation/multipeerconnectivity/mcnearbyserviceadvertiser/init(peer:discoveryinfo:servicetype:)">iOS</see>
    /// </remarks>
    public string ServiceName { get; set; } = AppInfo.Current.PackageName;

    /// <summary>
    /// Gets or sets additional information to include in the advertisement.
    /// TODO: If
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

#if ANDROID
    public required Activity Activity { get; init; }
#endif
}