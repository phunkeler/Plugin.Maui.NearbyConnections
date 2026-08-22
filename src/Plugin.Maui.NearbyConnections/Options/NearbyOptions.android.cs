namespace Plugin.Maui.NearbyConnections;

public sealed partial class NearbyOptions
{
    /// <summary>
    /// Maps <see cref="NearbyAndroidOptions.Topology"/> onto the Google Nearby Connections strategy
    /// it names.
    /// </summary>
    /// <remarks>
    /// The mapping is the whole point of the neutral enum: it keeps
    /// <c>Android.Gms.Nearby.Connection.Strategy</c> out of the public surface, so consumers never
    /// have to reference a vendor SDK type to configure the plugin.
    /// </remarks>
    internal Strategy ToPlatformStrategy()
        // `this.` is load-bearing: unqualified `Android` binds to the root Android namespace, not
        // to the options property, and the resulting error is nowhere near the cause.
        => this.Android.Topology switch
        {
            NearbyTopology.Star => Strategy.P2pStar,
            NearbyTopology.PointToPoint => Strategy.P2pPointToPoint,
            _ => Strategy.P2pCluster,
        };

    /// <summary>
    /// Maps <see cref="NearbyAndroidOptions.ConnectionType"/> onto the Google Nearby Connections
    /// constant it names.
    /// </summary>
    internal int ToPlatformConnectionType()
        // `this.` / `global::` are load-bearing here: the `Android` options property shadows the
        // root `Android` namespace inside this class, so both sides need disambiguating.
        => this.Android.ConnectionType switch
        {
            NearbyConnectionType.HighBandwidth => global::Android.Gms.Nearby.Connection.ConnectionType.Disruptive,
            NearbyConnectionType.NonDisruptive => global::Android.Gms.Nearby.Connection.ConnectionType.NonDisruptive,
            _ => global::Android.Gms.Nearby.Connection.ConnectionType.Balanced,
        };

    private static partial string GetDefaultDisplayName() => DeviceInfo.Name;
    private static partial string GetDefaultServiceId() => AppInfo.Name;
}
