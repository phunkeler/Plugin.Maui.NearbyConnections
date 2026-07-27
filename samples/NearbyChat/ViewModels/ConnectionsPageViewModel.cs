using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using Plugin.Maui.BottomSheet.Navigation;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class ConnectionsPageViewModel(
    IDispatcher dispatcher,
    INavigationService navigationService,
    INearbyAdvertiser advertiser,
    INearbyDiscoverer discoverer,
    IBottomSheetNavigationService bottomSheetNavigationService)
    : BasePageViewModel(dispatcher), IAdvertiserHandler, IDiscovererHandler
{
    public ObservableCollection<ConnectedDeviceViewModel> ConnectedDevices { get; } = [];

    IDispatcher? IAdvertiserHandler.Dispatcher => Dispatcher;
    IDispatcher? IDiscovererHandler.Dispatcher => Dispatcher;

    protected override void NavigatedTo()
    {
        base.NavigatedTo();

        _ = advertiser.EventsAsync(NavigationToken).RunAsync(this);
        _ = discoverer.EventsAsync(NavigationToken).RunAsync(this);
    }

    Task IAdvertiserHandler.OnConnectionAccepted(AdvertiserEvent.ConnectionAccepted ev)
    {
        if (ConnectedDevices.Any(vm => vm.Id == ev.Connection.RemoteDevice.Id))
        {
            return Task.CompletedTask;
        }

        var vm = new ConnectedDeviceViewModel(ev.Connection, bottomSheetNavigationService);
        ConnectedDevices.Add(vm);
        return Task.CompletedTask;
    }

    Task IAdvertiserHandler.OnConnectionDropped(AdvertiserEvent.ConnectionDropped ev)
    {
        var vm = ConnectedDevices.FirstOrDefault(d => d.Id == ev.Connection.RemoteDevice.Id);
        if (vm is not null)
        {
            ConnectedDevices.Remove(vm);
        }
        return Task.CompletedTask;
    }

    Task IDiscovererHandler.OnDeviceConnected(DiscovererEvent.DeviceConnected ev)
    {
        if (ConnectedDevices.Any(vm => vm.Id == ev.Connection.RemoteDevice.Id))
        {
            return Task.CompletedTask;
        }

        var vm = new ConnectedDeviceViewModel(ev.Connection, bottomSheetNavigationService);
        ConnectedDevices.Add(vm);
        return Task.CompletedTask;
    }

    Task IDiscovererHandler.OnDeviceDisconnected(DiscovererEvent.DeviceDisconnected ev)
    {
        var vm = ConnectedDevices.FirstOrDefault(d => d.Id == ev.Connection.RemoteDevice.Id);
        if (vm is not null)
        {
            ConnectedDevices.Remove(vm);
        }
        return Task.CompletedTask;
    }

    [RelayCommand]
    Task Back()
        => navigationService.GoBackAsync();
}
