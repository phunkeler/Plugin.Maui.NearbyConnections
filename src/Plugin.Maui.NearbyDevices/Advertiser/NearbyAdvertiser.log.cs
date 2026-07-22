namespace Plugin.Maui.NearbyDevices;

public sealed partial class NearbyAdvertiser
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Advertising started.")]
    partial void LogAdvertisingStarted();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Advertising stopped.")]
    partial void LogAdvertisingStopped();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connection request received from: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogConnectionRequested(string deviceId, string? displayName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connection accepted: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogConnectionAccepted(string deviceId, string? displayName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connection rejected: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogConnectionRejected(string deviceId, string? displayName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connection dropped: Id={DeviceId}, DisplayName={DisplayName}")]
    partial void LogConnectionDropped(string deviceId, string? displayName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ForwardPayloads error for connection Id={DeviceId}, DisplayName={DisplayName}.")]
    partial void LogForwardPayloadsError(string deviceId, string? displayName, Exception ex);
}
