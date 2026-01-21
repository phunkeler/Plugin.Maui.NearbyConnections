using CommunityToolkit.Mvvm.Input;
using NearbyChat.Extensions;
using NearbyChat.Services;
using System.ComponentModel;

namespace NearbyChat.ViewModels;

public partial class MainPageViewModel : BaseViewModel
{
    readonly AppShell _appShell;
    readonly INearbyConnectionsService _nearbyConnectionsService;

    public bool IsAdvertising => _nearbyConnectionsService.IsAdvertising;
    public bool IsDiscovering => _nearbyConnectionsService.IsDiscovering;

    public MainPageViewModel(AppShell appShell, INearbyConnectionsService nearbyConnectionsService)
    {
        ArgumentNullException.ThrowIfNull(appShell);
        ArgumentNullException.ThrowIfNull(nearbyConnectionsService);

        _appShell = appShell;
        _nearbyConnectionsService = nearbyConnectionsService;
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

    protected override void NavigatedTo()
    {
        OnPropertyChanged(nameof(IsAdvertising));
        OnPropertyChanged(nameof(IsDiscovering));
        _nearbyConnectionsService.PropertyChanged += OnServicePropertyChanged;
    }

    protected override void NavigatedFrom()
    {
        _nearbyConnectionsService.PropertyChanged -= OnServicePropertyChanged;
    }

    void OnServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(INearbyConnectionsService.IsAdvertising))
        {
            OnPropertyChanged(nameof(IsAdvertising));
        }

        if (e.PropertyName == nameof(INearbyConnectionsService.IsDiscovering))
        {
            OnPropertyChanged(nameof(IsDiscovering));
        }
    }
}