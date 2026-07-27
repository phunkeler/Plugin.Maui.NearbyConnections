using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class MainPageViewModel(
    IDispatcher dispatcher,
    INavigationService navigationService,
    INearbyAdvertiser advertiser,
    INearbyDiscoverer discoverer,
    IConnectionTracker connectionTracker)
    : BasePageViewModel(dispatcher)
{
    [ObservableProperty]
    public partial bool IsAdvertising { get; set; }

    [ObservableProperty]
    public partial bool IsDiscovering { get; set; }

    public IConnectionTracker Connections { get; } = connectionTracker;

    protected override void NavigatedTo()
    {
        base.NavigatedTo();

        IsAdvertising = advertiser.IsAdvertising;
        IsDiscovering = discoverer.IsDiscovering;
    }

    [RelayCommand]
    Task NavigateToAdvertising()
        => navigationService.GoToAsync<AdvertisingPageViewModel>();

    [RelayCommand]
    Task NavigateToDiscovery()
        => navigationService.GoToAsync<DiscoveryPageViewModel>();

    [RelayCommand]
    Task NavigateToConnections()
        => navigationService.GoToAsync<ConnectionsPageViewModel>();
}
