using Plugin.Maui.NearbyConnections.Advertise;
using AdvertiseOptions = Plugin.Maui.NearbyConnections.Advertise.AdvertiseOptions;

namespace Plugin.Maui.NearbyConnections.Discover;

/// <summary>
/// Options for configuring discovery behavior.
/// </summary>
public class DiscoverOptions
{
    /// <summary>
    /// Gets or sets the name of the service to discover.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Android:
    /// <a href="https://developers.google.com/android/reference/com/google/android/gms/nearby/connection/ConnectionsClient#public-abstract-taskvoid-startadvertising-string-name,-string-serviceid,-connectionlifecyclecallback-connectionlifecyclecallback,-advertisingoptions-options">
    ///     serviceId:
    /// </a>
    /// The ID for the service to be discovered, as specified in the corresponding call to <see cref="IAdvertiser.StartAdvertisingAsync(AdvertiseOptions)"/>
    /// </para>
    /// <para>
    /// iOS:
    /// <a href="https://developer.apple.com/documentation/multipeerconnectivity/mcnearbyserviceadvertiser/servicetype">
    ///     serviceType:
    /// </a>
    /// The type of service to search for. This should be a short text string that describes the app's networking protocol, in the same format as a Bonjour service type (without the transport protocol).
    /// </para>
    /// </remarks>
    public string ServiceName { get; set; } = AppInfo.Current.Name;

#if ANDROID

    Android.App.Activity? _activity;

    /// <summary>
    /// The <see cref="Android.App.Activity"/> used for automatically prompting resolvable connection error.
    /// </summary>
    public Android.App.Activity? Activity
    {
        get => _activity ?? Platform.CurrentActivity;
        set => _activity = value;
    }

#endif
}
