using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class MainPageViewModel : BasePageViewModel, IAdvertiserHandler, IDiscovererHandler
{
    readonly INavigationService _navigationService;
    readonly INearbyAdvertiser _advertiser;
    readonly INearbyDiscoverer _discoverer;

    [ObservableProperty]
    public partial bool IsAdvertising { get; set; }

    [ObservableProperty]
    public partial bool IsDiscovering { get; set; }

    [ObservableProperty]
    public partial int ConnectedDevicesCount { get; set; }

    IDispatcher? IAdvertiserHandler.Dispatcher => Dispatcher;
    IDispatcher? IDiscovererHandler.Dispatcher => Dispatcher;

    public MainPageViewModel(
        IDispatcher dispatcher,
        IMessenger messenger,
        INavigationService navigationService,
        INearbyAdvertiser advertiser,
        INearbyDiscoverer discoverer)
        : base(dispatcher, messenger)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(advertiser);
        ArgumentNullException.ThrowIfNull(discoverer);

        _navigationService = navigationService;
        _advertiser = advertiser;
        _discoverer = discoverer;
    }

    protected override void NavigatedTo()
    {
        IsAdvertising = _advertiser.IsAdvertising;
        IsDiscovering = _discoverer.IsDiscovering;

        _ = _advertiser.EventsAsync(NavigationToken).RunAsync(this);
        _ = _discoverer.EventsAsync(NavigationToken).RunAsync(this);

        base.NavigatedTo();
    }

    protected override void NavigatedFrom()
    {
        base.NavigatedFrom();
    }

    void IAdvertiserHandler.OnConnectionAccepted(AdvertiserEvent.ConnectionAccepted ev)
    {
        ConnectedDevicesCount++;
    }

    void IAdvertiserHandler.OnConnectionDropped(AdvertiserEvent.ConnectionDropped ev)
    {
        ConnectedDevicesCount--;
    }

    void IDiscovererHandler.OnDeviceConnected(DiscovererEvent.DeviceConnected ev)
    {
        ConnectedDevicesCount++;
    }

    void IDiscovererHandler.OnDeviceDisconnected(DiscovererEvent.DeviceDisconnected ev)
    {
        ConnectedDevicesCount--;
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
}
