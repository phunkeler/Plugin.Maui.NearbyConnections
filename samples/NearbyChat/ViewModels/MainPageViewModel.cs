using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class MainPageViewModel : BasePageViewModel
{
    readonly INavigationService _navigationService;
    readonly INearbyAdvertiser _advertiser;
    readonly INearbyDiscoverer _discoverer;

    NotifyCollectionChangedEventHandler? _advertiserConnectionsChangedHandler;
    NotifyCollectionChangedEventHandler? _discovererConnectionsChangedHandler;

    [ObservableProperty]
    public partial bool IsAdvertising { get; set; }

    [ObservableProperty]
    public partial bool IsDiscovering { get; set; }

    [ObservableProperty]
    public partial int ConnectedDevicesCount { get; set; }

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
        ConnectedDevicesCount = _advertiser.ActiveConnections.Count + _discoverer.ActiveConnections.Count;

        _advertiserConnectionsChangedHandler = OnConnectionsChanged;
        _discovererConnectionsChangedHandler = OnConnectionsChanged;

        if (_advertiser.ActiveConnections is INotifyCollectionChanged advertiserNotify)
            advertiserNotify.CollectionChanged += _advertiserConnectionsChangedHandler;

        if (_discoverer.ActiveConnections is INotifyCollectionChanged discovererNotify)
            discovererNotify.CollectionChanged += _discovererConnectionsChangedHandler;

        base.NavigatedTo();
    }

    protected override void NavigatedFrom()
    {
        if (_advertiser.ActiveConnections is INotifyCollectionChanged advertiserNotify && _advertiserConnectionsChangedHandler is not null)
            advertiserNotify.CollectionChanged -= _advertiserConnectionsChangedHandler;

        if (_discoverer.ActiveConnections is INotifyCollectionChanged discovererNotify && _discovererConnectionsChangedHandler is not null)
            discovererNotify.CollectionChanged -= _discovererConnectionsChangedHandler;

        _advertiserConnectionsChangedHandler = null;
        _discovererConnectionsChangedHandler = null;

        base.NavigatedFrom();
    }

    void OnConnectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.DispatchAsync(() =>
        {
            ConnectedDevicesCount = _advertiser.ActiveConnections.Count + _discoverer.ActiveConnections.Count;
        });
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
