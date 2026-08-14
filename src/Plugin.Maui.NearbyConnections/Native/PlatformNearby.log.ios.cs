namespace Plugin.Maui.NearbyConnections;

// EventIds here belong to the PlatformNearby range (2000-2099) declared in PlatformNearby.log.cs.
// These two live in an iOS-only partial because they take native/internal enum parameters rather
// than pre-formatted strings: the generator formats an enum only after its IsEnabled guard passes,
// so an Enum.GetName at the call site would allocate on every inbound message with logging off.
sealed partial class PlatformNearby
{
    [LoggerMessage(EventId = 2052, Level = LogLevel.Debug, Message = "Peer state changed: Id={DeviceId}, DisplayName={DisplayName}, State={State}")]
    partial void LogPeerStateChanged(string deviceId, string displayName, MCSessionState state);

    [LoggerMessage(EventId = 2054, Level = LogLevel.Trace, Message = "Control message received from peer: Id={DeviceId}, DisplayName={DisplayName}, Type={Type}")]
    partial void LogControlMessageReceived(string deviceId, string displayName, ControlMessageType type);
}