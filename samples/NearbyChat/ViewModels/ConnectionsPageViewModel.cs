using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class ConnectionsPageViewModel : BasePageViewModel, IAdvertiserHandler, IDiscovererHandler
{
    readonly INavigationService _navigationService;
    readonly INearbyAdvertiser _advertiser;
    readonly INearbyDiscoverer _discoverer;
    readonly INearbyDeviceViewModelFactory _nearbyDeviceViewModelFactory;

    public ObservableCollection<ConnectedDeviceViewModel> ConnectedDevices { get; } = [];

    IDispatcher? IAdvertiserHandler.Dispatcher => Dispatcher;
    IDispatcher? IDiscovererHandler.Dispatcher => Dispatcher;

    public ConnectionsPageViewModel(
        IDispatcher dispatcher,
        IMessenger messenger,
        INavigationService navigationService,
        INearbyAdvertiser advertiser,
        INearbyDiscoverer discoverer,
        INearbyDeviceViewModelFactory nearbyDeviceViewModelFactory)
        : base(dispatcher, messenger)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(advertiser);
        ArgumentNullException.ThrowIfNull(discoverer);
        ArgumentNullException.ThrowIfNull(nearbyDeviceViewModelFactory);

        _navigationService = navigationService;
        _advertiser = advertiser;
        _discoverer = discoverer;
        _nearbyDeviceViewModelFactory = nearbyDeviceViewModelFactory;
    }

    protected override void NavigatedTo()
    {
        base.NavigatedTo();

        _ = _advertiser.EventsAsync(NavigationToken).RunAsync(this);
        _ = _discoverer.EventsAsync(NavigationToken).RunAsync(this);
    }

    protected override void NavigatedFrom()
    {
        base.NavigatedFrom();
    }

    void IAdvertiserHandler.OnConnectionAccepted(AdvertiserEvent.ConnectionAccepted ev)
    {
        if (ConnectedDevices.Any(vm => vm.Id == ev.Connection.RemoteDevice.Id))
        {
            return;
        }

        var vm = _nearbyDeviceViewModelFactory.CreateConnected(ev.Connection);
        vm.IsActive = true;
        ConnectedDevices.Add(vm);
    }

    void IAdvertiserHandler.OnConnectionDropped(AdvertiserEvent.ConnectionDropped ev)
    {
        var vm = ConnectedDevices.FirstOrDefault(d => d.Id == ev.Connection.RemoteDevice.Id);
        if (vm is not null)
        {
            vm.IsActive = false;
            ConnectedDevices.Remove(vm);
        }
    }

    void IDiscovererHandler.OnDeviceConnected(DiscovererEvent.DeviceConnected ev)
    {
        if (ConnectedDevices.Any(vm => vm.Id == ev.Connection.RemoteDevice.Id))
        {
            return;
        }

        var vm = _nearbyDeviceViewModelFactory.CreateConnected(ev.Connection);
        vm.IsActive = true;
        ConnectedDevices.Add(vm);
    }

    void IDiscovererHandler.OnDeviceDisconnected(DiscovererEvent.DeviceDisconnected ev)
    {
        var vm = ConnectedDevices.FirstOrDefault(d => d.Id == ev.Connection.RemoteDevice.Id);
        if (vm is not null)
        {
            vm.IsActive = false;
            ConnectedDevices.Remove(vm);
        }
    }

    [RelayCommand]
    Task Back()
        => _navigationService.GoBackAsync();
}
