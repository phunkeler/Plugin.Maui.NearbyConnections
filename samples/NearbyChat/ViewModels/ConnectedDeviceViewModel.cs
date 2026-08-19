using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Plugin.Maui.BottomSheet.Navigation;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

/// <summary>
/// A connected device row.
/// </summary>
/// <remarks>
/// Takes the session rather than a <see cref="NearbyConnection"/>, so it can be projected from a
/// <see cref="NearbyDeviceCollection{TRow}"/> like every other row type. The connection is looked
/// up per command instead of captured: a device is a value and cannot hold a live handle, and a
/// captured handle would go stale the moment the peer dropped and reconnected.
/// </remarks>
public partial class ConnectedDeviceViewModel : NearbyDeviceViewModel
{
    readonly INearby _nearby;
    readonly IBottomSheetNavigationService _bottomSheetNavigationService;
    readonly ILogger _logger;

    public ConnectedDeviceViewModel(
        NearbyDevice device,
        INearby nearby,
        IBottomSheetNavigationService bottomSheetNavigationService,
        ILogger logger)
        : base(device)
    {
        ArgumentNullException.ThrowIfNull(nearby);
        ArgumentNullException.ThrowIfNull(bottomSheetNavigationService);
        ArgumentNullException.ThrowIfNull(logger);

        _nearby = nearby;
        _bottomSheetNavigationService = bottomSheetNavigationService;
        _logger = logger;
    }

    [RelayCommand]
    async Task Chat()
    {
        // The result is the only failure signal: NavigateToAsync reports a failed open by
        // returning Success = false rather than throwing, so discarding it means a tap that
        // opens nothing and says nothing.
        var result = await _bottomSheetNavigationService.NavigateToAsync(
            nameof(ChatViewModel),
            new BottomSheetNavigationParameters
            {
                { nameof(NearbyDevice), Device }
            });

        if (result.Success || result.Cancelled)
        {
            return;
        }

        LogChatOpenFailed(_logger, Device.Id, result.Exception);
    }

    [RelayCommand]
    async Task Disconnect()
    {
        // Already gone if the lookup fails — the row is about to leave the collection anyway,
        // because the device stops matching the Connected filter.
        if (_nearby.TryGetConnection(Device.Id, out var connection))
        {
            await connection.DisposeAsync();
        }
    }

    [LoggerMessage(
        EventId = 1,
        EventName = nameof(LogChatOpenFailed),
        Level = LogLevel.Error,
        Message = "Opening the chat sheet for {DeviceId} failed.")]
    static partial void LogChatOpenFailed(ILogger logger, string deviceId, Exception? exception);
}
