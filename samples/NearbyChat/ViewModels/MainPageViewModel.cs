using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Messages;
using NearbyChat.Services;

namespace NearbyChat.ViewModels;

public partial class MainPageViewModel : BasePageViewModel,
    IRecipient<AdvertisingStateChangedMessage>,
    IRecipient<DiscoveringStateChangedMessage>
{
    readonly INavigationService _navigationService;
    readonly INearbyConnectionsService _nearbyConnectionsService;

    [ObservableProperty]
    public partial bool IsAdvertising { get; set; }

    [ObservableProperty]
    public partial bool IsDiscovering { get; set; }

    public MainPageViewModel(
        IDispatcher dispatcher,
        IMessenger messenger,
        INavigationService navigationService,
        INearbyConnectionsService nearbyConnectionsService)
        : base(dispatcher, messenger)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(nearbyConnectionsService);

        _navigationService = navigationService;
        _nearbyConnectionsService = nearbyConnectionsService;
    }

    protected override void NavigatedTo()
    {
        IsAdvertising = _nearbyConnectionsService.IsAdvertising;
        IsDiscovering = _nearbyConnectionsService.IsDiscovering;
        base.NavigatedTo();
    }

    [RelayCommand]
    Task NavigateToAdvertising()
        => _navigationService.GoToAsync<AdvertisingPageViewModel>();

    [RelayCommand]
    Task NavigateToDiscovery()
        => _navigationService.GoToAsync<DiscoveryPageViewModel>();

    [RelayCommand]
    Task NavigateToConnections()
        => _navigationService.GoToAsync<ConnectionsPageViewModel>();

    public void Receive(AdvertisingStateChangedMessage message)
        => IsAdvertising = message.Value;

    public void Receive(DiscoveringStateChangedMessage message)
        => IsDiscovering = message.Value;
}