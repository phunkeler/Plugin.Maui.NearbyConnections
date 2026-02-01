using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Messages;
using NearbyChat.Services;

namespace NearbyChat.ViewModels;

public partial class DiscoveryPageViewModel : BasePageViewModel,
    IRecipient<DiscoveringStateChangedMessage>,
    IRecipient<DeviceFoundMessage>,
    IRecipient<DeviceLostMessage>
{
    readonly INavigationService _navigationService;
    readonly INearbyConnectionsService _nearbyConnectionsService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleDiscoveryCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsDiscovering { get; set; }

    public ObservableCollection<DiscoveredDeviceViewModel> DiscoveredDevices { get; } = [];

    public DiscoveryPageViewModel(
        IMessenger messenger,
        INavigationService navigationService,
        INearbyConnectionsService nearbyConnectionsService)
        : base(messenger)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(nearbyConnectionsService);

        _navigationService = navigationService;
        _nearbyConnectionsService = nearbyConnectionsService;

        IsDiscovering = _nearbyConnectionsService.IsDiscovering;

        foreach (var discovered in _nearbyConnectionsService.DiscoveredDevices)
        {
            DiscoveredDevices.Add(new DiscoveredDeviceViewModel(discovered, _nearbyConnectionsService));
        }
    }

    [RelayCommand]
    async Task Back()
    {
        await _navigationService.GoBackAsync();
    }

    [RelayCommand(CanExecute = nameof(CanToggleDiscovery))]
    async Task ToggleDiscovery(CancellationToken cancellationToken)
    {
        IsBusy = true;

        try
        {
            if (IsDiscovering)
            {
                await _nearbyConnectionsService.StopDiscoveryAsync(cancellationToken);
            }
            else
            {
                await _nearbyConnectionsService.StartDiscoveryAsync(cancellationToken);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Receive(DiscoveringStateChangedMessage message)
        => IsDiscovering = message.Value;

    public void Receive(DeviceFoundMessage message)
    {
        if (DiscoveredDevices.Any(d => d.Id == message.Value.Id))
            return;

        var discoveredDevice = new DiscoveredDevice(message.Value, message.FoundAt);
        DiscoveredDevices.Add(new DiscoveredDeviceViewModel(discoveredDevice, _nearbyConnectionsService));
    }

    public void Receive(DeviceLostMessage message)
    {
        var device = DiscoveredDevices.FirstOrDefault(d => d.Id == message.Value.Id);
        if (device is not null)
        {
            DiscoveredDevices.Remove(device);
        }
    }

    bool CanToggleDiscovery() => !IsBusy;
}