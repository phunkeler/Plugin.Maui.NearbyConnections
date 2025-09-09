namespace Plugin.Maui.NearbyConnections.Device;

/// <inheritdoc/>
public sealed class NearbyDevice(string id, string displayName) : INearbyDevice
{
    /// <inheritdoc/>
    public string Id { get; } = id;

    /// <inheritdoc/>
    public string DisplayName { get; } = displayName;
}