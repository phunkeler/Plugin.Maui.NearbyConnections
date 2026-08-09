namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents data sent to, or received from, a nearby device.
/// </summary>
/// <remarks>
/// This is the base type for all payloads. The library produces and accepts two concrete payload
/// types: <see cref="NearbyBytesPayload"/> and <see cref="NearbyFilePayload"/>.
/// </remarks>
/// <seealso cref="NearbyBytesPayload"/>
/// <seealso cref="NearbyFilePayload"/>
public abstract record NearbyPayload;
