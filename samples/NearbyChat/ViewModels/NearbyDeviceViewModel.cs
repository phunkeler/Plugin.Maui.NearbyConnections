using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Messages;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public abstract partial class NearbyDeviceViewModel : ObservableRecipient, IRecipient<DeviceStateChangedMessage>
{
    protected NearbyDevice Device { get; }

    public string Id => Device.Id;
    public string DisplayName => Device.DisplayName ?? "Unknown";

    [ObservableProperty]
    public partial NearbyDeviceState State { get; set; }

    public NearbyDeviceViewModel(NearbyDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        Device = device;
        State = device.State;
    }

    public void Receive(DeviceStateChangedMessage message)
    {
        if (message.Value.Id == Device.Id)
        {
            State = message.Value.State;
        }
    }
}