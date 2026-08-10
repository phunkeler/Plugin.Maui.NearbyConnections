using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using Plugin.Maui.BottomSheet.Navigation;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class ConnectionsPageViewModel(
    IDispatcher dispatcher,
    INavigationService navigationService,
    INearby session,
    IBottomSheetNavigationService bottomSheetNavigationService)
    : BasePageViewModel(dispatcher)
{
    public ObservableCollection<ConnectedDeviceViewModel> ConnectedDevices { get; } = [];

    protected override void NavigatedTo()
    {
        base.NavigatedTo();

        // Connections made while this page was away are already in Devices.
        ConnectedDevices.Clear();

        foreach (var device in session.Devices)
        {
            if (session.TryGetConnection(device.Id, out var connection))
            {
                Add(device.Id, connection);
            }
        }

        _ = WatchDevicesAsync(NavigationToken);
    }

    /// <summary>
    /// Tracks connections coming and going until the page is navigated away from.
    /// </summary>
    async Task WatchDevicesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var change in session.Devices.Changes.WithCancellation(cancellationToken))
            {
                var device = change.Device;

                // The connection is looked up rather than carried on the change: a device is a
                // value and cannot hold a live handle.
                var connection = change.Action is not NearbyDeviceChangeAction.Removed
                    && device.Status is NearbyDeviceStatus.Connected
                    && session.TryGetConnection(device.Id, out var found)
                        ? found
                        : null;

                // Changes arrive on a platform background thread; ConnectedDevices is bound.
                await Dispatcher.DispatchAsync(() =>
                {
                    if (connection is not null)
                    {
                        Add(device.Id, connection);
                    }
                    else if (ConnectedDevices.FirstOrDefault(d => d.Id == device.Id) is { } vm)
                    {
                        ConnectedDevices.Remove(vm);
                    }
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Navigated away.
        }
    }

    void Add(string deviceId, NearbyConnection connection)
    {
        if (ConnectedDevices.Any(vm => vm.Id == deviceId))
        {
            return;
        }

        ConnectedDevices.Add(new ConnectedDeviceViewModel(connection, bottomSheetNavigationService));
    }

    [RelayCommand]
    Task Back()
        => navigationService.GoBackAsync();
}
