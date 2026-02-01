using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Services;

public record DiscoveredDevice(NearbyDevice Device, DateTimeOffset FoundAt);
