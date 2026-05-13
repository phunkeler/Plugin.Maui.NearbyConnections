using CommunityToolkit.Mvvm.Input;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class DiscoveredDeviceViewModel(
    NearbyDevice device,
    INearbyDiscoverer discoverer) : NearbyDeviceViewModel(device)
{
    bool _isConnecting;
    public bool IsConnecting
    {
        get => _isConnecting;
        private set
        {
            if (SetProperty(ref _isConnecting, value))
            {
                OnPropertyChanged(nameof(StateGlyph));
                OnPropertyChanged(nameof(StateColor));
            }
        }
    }

    public override string StateGlyph => IsConnecting
        ? Resource<string>("icon-link")
        : Resource<string>("icon-magnify");

    public override Color StateColor => IsConnecting
        ? Resource<Color>("StatusInfo")
        : Resource<Color>("LightTextQuaternary");

    [RelayCommand]
    async Task Connect()
    {
        IsConnecting = true;
        try
        {
            await discoverer.ConnectAsync(Device);
        }
        catch
        {
            IsConnecting = false;
            throw;
        }
    }
}
