using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class DiscoveredDeviceViewModel(
    NearbyDevice device,
    INearbyDiscoverer discoverer) : NearbyDeviceViewModel(device)
{
    [ObservableProperty]
    public partial bool IsConnecting { get; private set; }

    [RelayCommand]
    async Task Connect()
    {
        IsConnecting = true;
        try
        {
            await discoverer.ConnectAsync(Device);
        }
        catch (NearbyConnectionsException)
        {
            IsConnecting = false;
        }
        catch
        {
            IsConnecting = false;
            throw;
        }
    }
}
