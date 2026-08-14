namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Represents data sent to, or received from, a nearby device.
/// </summary>
/// <remarks>
/// This is the abstract base for every payload shape the library produces or accepts:
/// <see cref="NearbyBytesPayload"/> and <see cref="NearbyFilePayload"/>.
/// </remarks>
/// <seealso cref="NearbyBytesPayload"/>
/// <seealso cref="NearbyFilePayload"/>
public abstract record NearbyPayload;