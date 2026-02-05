using Plugin.Maui.NearbyConnections;

namespace NearbyChat.Models;

public record AdvertisedDevice(NearbyDevice Device, DateTimeOffset InvitedAt);
