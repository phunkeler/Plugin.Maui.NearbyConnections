using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Models;

public record DiscoveredDevice(NearbyDevice Device, DateTimeOffset FoundAt);
