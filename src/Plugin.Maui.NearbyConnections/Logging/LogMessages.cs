namespace Plugin.Maui.NearbyConnections.Logging;

internal static partial class LogMessages
{
    #region Discovery

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Debug,
        Message = "Starting discovery with ServiceName={ServiceName}")]
    public static partial void StartingDiscovery(this ILogger logger, string serviceName);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Debug,
        Message = "Discovery started successfully")]
    public static partial void DiscoveryStarted(this ILogger logger);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Debug,
        Message = "Stopping discovery")]
    public static partial void StoppingDiscovery(this ILogger logger);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Debug,
        Message = "Discovery stopped")]
    public static partial void DiscoveryStopped(this ILogger logger);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Error,
        Message = "Failed to start discovery")]
    public static partial void DiscoveryStartFailed(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Warning,
        Message = "The plugin is already discovering; call 'StopDiscoveryAsync()' before trying to start discovery again.")]
    public static partial void AlreadyDiscovering(this ILogger logger);

    #endregion Discovery

    #region Advertising

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Debug,
        Message = "Starting advertising with service ID '{ServiceId}' and endpoint name '{EndpointName}'")]
    public static partial void StartingAdvertising(this ILogger logger, string serviceId, string endpointName);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Debug,
        Message = "Advertising started successfully")]
    public static partial void AdvertisingStarted(this ILogger logger);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Debug,
        Message = "Stopping advertising")]
    public static partial void StoppingAdvertising(this ILogger logger);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Debug,
        Message = "Advertising stopped")]
    public static partial void AdvertisingStopped(this ILogger logger);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Error,
        Message = "Failed to start advertising")]
    public static partial void AdvertisingStartFailed(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Warning,
        Message = "The plugin is already advertising; call 'StopAdvertisingAsync()' before trying to start advertising again.")]
    public static partial void AlreadyAdvertising(this ILogger logger);

    #endregion Advertising

    #region Event Processing

    [LoggerMessage(
        EventId = 5000,
        Level = LogLevel.Error,
        Message = "Exception in event subscriber for event '{EventId}'")]
    public static partial void EventSubscriberException(this ILogger logger, string eventId, Exception exception);

    #endregion Event Processing

    #region Disposal

    [LoggerMessage(
        EventId = 7000,
        Level = LogLevel.Debug,
        Message = "Disposing NearbyConnections")]
    public static partial void Disposing(this ILogger logger);

    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Warning,
        Message = "Failed to acquire semaphore during disposal")]
    public static partial void DisposeSemaphoreFailure(this ILogger logger, Exception exception);

    #endregion Disposal
}
