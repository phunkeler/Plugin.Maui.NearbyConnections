using CommunityToolkit.Mvvm.Input;
using NearbyChat.Extensions;

namespace NearbyChat.ViewModels;

public partial class MainPageViewModel : BaseViewModel
{
    readonly AppShell _appShell;

    public MainPageViewModel(AppShell appShell)
    {
        _appShell = appShell;
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
}