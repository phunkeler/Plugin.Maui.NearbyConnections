using Microsoft.Extensions.Logging;

namespace Plugin.Maui.NearbyConnections.Logging;

internal static partial class LogMessages
{
    #region  Discovery

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Information,
        Message = "FoundPeer: DisplayName={DisplayName}")]
    public static partial void FoundPeer(this ILogger logger, string displayName);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Information,
        Message = "LostPeer: DisplayName={DisplayName}")]
    public static partial void LostPeer(this ILogger logger, string displayName);

    #endregion Discovery

    #region Advertising

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Error,
        Message = "Did not start advertising: ServiceType={ServiceType}, DisplayName={DisplayName}, Error={Error}")]
    public static partial void DidNotStartAdvertisingPeer(this ILogger logger, string serviceType, string displayName, string error);

    [LoggerMessage(
        EventId = 2007,
        Level = LogLevel.Information,
        Message = "Received invitation from peer: DisplayName={DisplayName}")]
    public static partial void DidReceiveInvitationFromPeer(this ILogger logger, string displayName);

    #endregion Advertising
}
