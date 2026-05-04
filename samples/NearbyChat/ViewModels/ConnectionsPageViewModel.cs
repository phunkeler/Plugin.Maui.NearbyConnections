using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Messages;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class ConnectionsPageViewModel : BasePageViewModel,
    IRecipient<DeviceDisconnectedMessage>,
    IRecipient<ConnectionResponseMessage>
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

    }

    protected override void NavigatedTo()
    {
        base.NavigatedTo();

        var connected = _nearbyConnectionsService.Devices
            .Where(d => d.State == NearbyDeviceState.Connected)
            .ToList();

        var toRemove = ConnectedDevices
            .Where(vm => !connected.Any(d => d.Id == vm.Id))
            .ToList();

        foreach (var vm in toRemove)
        {
            vm.IsActive = false;
            ConnectedDevices.Remove(vm);
        }

        foreach (var device in connected.Where(d => !ConnectedDevices.Any(vm => vm.Id == d.Id)))
        {
            var vm = _nearbyDeviceViewModelFactory.CreateConnected(device);
            vm.IsActive = true;
            ConnectedDevices.Add(vm);
        }
    }

    public void Receive(DeviceDisconnectedMessage message)
    {
        var vm = ConnectedDevices.FirstOrDefault(d => d.Id == message.Value.Id);
        if (vm is not null)
            ConnectedDevices.Remove(vm);
    }

    public void Receive(ConnectionResponseMessage message)
    {
        if (!message.Accepted || ConnectedDevices.Any(d => d.Id == message.Value.Id))
            return;

        var vm = _nearbyDeviceViewModelFactory.CreateConnected(message.Value);
        vm.IsActive = true;
        ConnectedDevices.Add(vm);
    }

    [RelayCommand]
    Task Back()
        => _navigationService.GoBackAsync();
}
