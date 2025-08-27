namespace Plugin.Maui.NearbyConnections;

public class NearbyConnectionOptions
{
    public required string DisplayName { get; init; }
    public required string ServiceName { get; init; }
    public bool ManualAdvertising { get; init; } = true;
    public bool ManualDiscovery { get; init; } = true;
    public bool AutoAcceptConnections { get; init; }
}