using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class ConnectionsPageViewModel : BasePageViewModel
{
    readonly INavigationService _navigationService;
    readonly INearbyConnectionsService _nearbyConnectionsService;
    readonly INearbyDeviceViewModelFactory _nearbyDeviceViewModelFactory;

    public ObservableCollection<ConnectedDeviceViewModel> ConnectedDevices { get; } = [];

    public ConnectionsPageViewModel(
        IDispatcher dispatcher,
        IMessenger messenger,
        INavigationService navigationService,
        INearbyConnectionsService nearbyConnectionsService,
        INearbyDeviceViewModelFactory nearbyDeviceViewModelFactory)
        : base(dispatcher, messenger)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(nearbyConnectionsService);
        ArgumentNullException.ThrowIfNull(nearbyDeviceViewModelFactory);

        _navigationService = navigationService;
        _nearbyConnectionsService = nearbyConnectionsService;
        _nearbyDeviceViewModelFactory = nearbyDeviceViewModelFactory;

        foreach (var connectedDevice in _nearbyConnectionsService.Devices.Where(d => d.State == NearbyDeviceState.Connected))
        {
            var vm = _nearbyDeviceViewModelFactory.CreateConnected(connectedDevice);
            vm.IsActive = true;
            ConnectedDevices.Add(vm);
        }
    }

    [RelayCommand]
    Task Back()
        => _navigationService.GoBackAsync();
}