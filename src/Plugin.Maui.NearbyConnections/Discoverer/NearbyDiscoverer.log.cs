namespace Plugin.Maui.NearbyConnections;

public sealed partial class NearbyDiscoverer
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Discovery started.")]
    partial void LogDiscoveryStarted();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Discovery stopped.")]
    partial void LogDiscoveryStopped();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Device found: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogDeviceFound(string deviceId, string? displayName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Device lost: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogDeviceLost(string deviceId, string? displayName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connecting to device: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogConnecting(string deviceId, string? displayName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connected to device: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogConnected(string deviceId, string? displayName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connection dropped: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogConnectionDropped(string deviceId, string? displayName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ForwardPayloads error for connection Id={DeviceId}, DisplayName={DisplayName}.")]
    partial void LogForwardPayloadsError(string deviceId, string? displayName, Exception ex);
}
