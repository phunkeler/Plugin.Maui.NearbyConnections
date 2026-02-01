using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Extensions;
using NearbyChat.Messages;
using NearbyChat.Services;

namespace NearbyChat.ViewModels;

public partial class MainPageViewModel : BasePageViewModel,
    IRecipient<AdvertisingStateChangedMessage>,
    IRecipient<DiscoveringStateChangedMessage>
{
    readonly AppShell _appShell;
    readonly INearbyConnectionsService _nearbyConnectionsService;

    [ObservableProperty]
    public partial bool IsAdvertising { get; set; }

    [ObservableProperty]
    public partial bool IsDiscovering { get; set; }

    public MainPageViewModel(
        IMessenger messenger,
        AppShell appShell,
        INearbyConnectionsService nearbyConnectionsService)
        : base(messenger)
    {
        ArgumentNullException.ThrowIfNull(appShell);
        ArgumentNullException.ThrowIfNull(nearbyConnectionsService);

        _appShell = appShell;
        _nearbyConnectionsService = nearbyConnectionsService;
    }

    protected override void NavigatedTo()
    {
        IsAdvertising = _nearbyConnectionsService.IsAdvertising;
        IsDiscovering = _nearbyConnectionsService.IsDiscovering;
        base.NavigatedTo();
    }

    [RelayCommand]
    async Task NavigateToAdvertising()
        => await _appShell.GoToAsync<AdvertisingPageViewModel>();

    [RelayCommand]
    async Task NavigateToDiscovery()
        => await _appShell.GoToAsync<DiscoveryPageViewModel>();

    [RelayCommand]
    async Task NavigateToConnections()
        => await _appShell.GoToAsync<ConnectionsPageViewModel>();

    public void Receive(AdvertisingStateChangedMessage message)
    {
        IsAdvertising = message.Value;
    }

    public void Receive(DiscoveringStateChangedMessage message)
    {
        IsDiscovering = message.Value;
    }
}