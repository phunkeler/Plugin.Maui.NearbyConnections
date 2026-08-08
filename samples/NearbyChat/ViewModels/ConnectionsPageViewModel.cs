using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using Plugin.Maui.BottomSheet.Navigation;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class ConnectionsPageViewModel(
    IDispatcher dispatcher,
    INavigationService navigationService,
    INearbyConnections session,
    IBottomSheetNavigationService bottomSheetNavigationService)
    : BasePageViewModel(dispatcher)
{
    public ObservableCollection<ConnectedDeviceViewModel> ConnectedDevices { get; } = [];

    protected override void NavigatedTo()
    {
        base.NavigatedTo();

        // Two handlers where there were four: established/dropped are now one pair of events rather
        // than one pair per role.
        RegisterSessionSubscription(
            () => session.ConnectionEstablished += OnConnectionEstablished,
            () => session.ConnectionEstablished -= OnConnectionEstablished);

        RegisterSessionSubscription(
            () => session.ConnectionDropped += OnConnectionDropped,
            () => session.ConnectionDropped -= OnConnectionDropped);

        // Connections made while this page was away are already in Devices.
        ConnectedDevices.Clear();

        foreach (var device in session.Devices.Where(d => d.Status is NearbyDeviceStatus.Connected))
        {
            if (device.Connection is { } connection)
            {
                Add(device.Id, connection);
            }
        }
    }

    void OnConnectionEstablished(object? sender, NearbyConnectionChangedEventArgs e)
        => Add(e.Device.Id, e.Connection);

    void OnConnectionDropped(object? sender, NearbyConnectionChangedEventArgs e)
    {
        if (ConnectedDevices.FirstOrDefault(d => d.Id == e.Device.Id) is { } vm)
        {
            ConnectedDevices.Remove(vm);
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
