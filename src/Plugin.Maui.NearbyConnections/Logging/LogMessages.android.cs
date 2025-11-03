using Microsoft.Extensions.Logging;

namespace Plugin.Maui.NearbyConnections.Logging;

internal static partial class LogMessages
{
    #region Advertising

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Information,
        Message = "Connection initiated: EndpointId={EndpointId}, EndpointName={EndpointName}, IsIncoming={IsIncoming}")]
    public static partial void ConnectionInitiated(
        this ILogger logger,
        string endpointId,
        string endpointName,
        bool isIncoming);

    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Information,
        Message = "Connection result: EndpointId={EndpointId}, StatusCode={StatusCode}, Message={Message}, IsSuccess={IsSuccess}")]
    public static partial void OnConnectionResult(
        this ILogger logger,
        string endpointId,
        int statusCode,
        string message,
        bool isSuccess);

    [LoggerMessage(
        EventId = 2008,
        Level = LogLevel.Information,
        Message = "Disconnected from EndpointId={EndpointId}")]
    public static partial void Disconnected(this ILogger logger, string endpointId);

    #endregion Advertising

    #region Discovery

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Information,
        Message = "Endpoint found: EndpointId={EndpointId}, EndpointName={EndpointName}")]
    public static partial void EndpointFound(
        this ILogger logger,
        string endpointId,
        string endpointName);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Information,
        Message = "Endpoint lost: EndpointId={EndpointId}")]
    public static partial void EndpointLost(this ILogger logger, string endpointId);

    #endregion Discovery
}