namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// A single change to the set of devices known to an <see cref="INearby"/>.
/// </summary>
/// <param name="Action">What happened to the device.</param>
/// <param name="Device">
/// The device as it is after the change. This is a snapshot: it does not update as the device
/// changes again, and a later change carries a new instance.
/// </param>
/// <remarks>
/// Changes are deltas rather than whole-list snapshots, so a consumer applies one change instead of
/// re-diffing the collection on every transition.
/// </remarks>
public sealed record NearbyDeviceChange(NearbyDeviceChangeAction Action, NearbyDevice Device);
