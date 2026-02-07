using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Messages;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public abstract partial class NearbyDeviceViewModel : ObservableRecipient,
    IRecipient<DeviceStateChangedMessage>
{
    protected NearbyDevice Device { get; }
    protected INearbyConnectionsService NearbyConnectionsService { get; }
    protected IDispatcher Dispatcher { get; }

    public string Id => Device.Id;
    public string DisplayName => Device.DisplayName ?? "Unknown";
    public DateTimeOffset LastSeenAt => Device.LastSeenAt;

    [ObservableProperty]
    public partial NearbyDeviceState State { get; set; }

    public NearbyDeviceViewModel(
        NearbyDevice device,
        INearbyConnectionsService nearbyConnectionsService,
        IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(nearbyConnectionsService);
        ArgumentNullException.ThrowIfNull(dispatcher);

        Device = device;
        State = device.State;
        NearbyConnectionsService = nearbyConnectionsService;
        Dispatcher = dispatcher;
    }

    public async void Receive(DeviceStateChangedMessage message)
        => await Dispatcher.DispatchAsync(() =>
        {
            if (message.Value.Id == Device.Id)
            {
                State = message.Value.State;
            }
        });

    public void RefreshRelativeTime() => OnPropertyChanged(nameof(LastSeenAt));

}