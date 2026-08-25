namespace Plugin.Maui.NearbyConnections;

// EventIds here belong to the platform-layer range (2000-2099) declared in PlatformBridge.log.cs.
// These live on the iOS adapter because they take native/internal enum parameters rather than
// pre-formatted strings: the generator formats an enum only after its IsEnabled guard passes, so
// an Enum.GetName at the call site would allocate on every inbound message with logging off.
sealed partial class IosAdapter
{
    [LoggerMessage(EventId = 2052, Level = LogLevel.Debug, Message = "Peer state changed: Id={DeviceId}, DisplayName={DisplayName}, State={State}")]
    internal partial void LogPeerStateChanged(string deviceId, string? displayName, MCSessionState state);

    [LoggerMessage(EventId = 2054, Level = LogLevel.Trace, Message = "Control message received from peer: Id={DeviceId}, DisplayName={DisplayName}, Type={Type}")]
    internal partial void LogControlMessageReceived(string deviceId, string? displayName, ControlMessageType type);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Debug, Message = "Created local peer: DisplayName={DisplayName}")]
    internal partial void LogCreatedLocalPeer(string displayName);
}