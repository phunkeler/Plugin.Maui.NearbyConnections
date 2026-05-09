using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class ConnectionsPageViewModel : BasePageViewModel
{
    readonly INavigationService _navigationService;
    readonly INearbyAdvertiser _advertiser;
    readonly INearbyDiscoverer _discoverer;
    readonly INearbyDeviceViewModelFactory _nearbyDeviceViewModelFactory;

    NotifyCollectionChangedEventHandler? _advertiserConnectionsChangedHandler;
    NotifyCollectionChangedEventHandler? _discovererConnectionsChangedHandler;

    public ObservableCollection<ConnectedDeviceViewModel> ConnectedDevices { get; } = [];

    public ConnectionsPageViewModel(
        IDispatcher dispatcher,
        IMessenger messenger,
        INavigationService navigationService,
        INearbyAdvertiser advertiser,
        INearbyDiscoverer discoverer,
        INearbyDeviceViewModelFactory nearbyDeviceViewModelFactory)
        : base(dispatcher, messenger)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(advertiser);
        ArgumentNullException.ThrowIfNull(discoverer);
        ArgumentNullException.ThrowIfNull(nearbyDeviceViewModelFactory);

        _navigationService = navigationService;
        _advertiser = advertiser;
        _discoverer = discoverer;
        _nearbyDeviceViewModelFactory = nearbyDeviceViewModelFactory;
    }

    protected override void NavigatedTo()
    {
        base.NavigatedTo();

        _advertiserConnectionsChangedHandler = OnConnectionsChanged;
        _discovererConnectionsChangedHandler = OnConnectionsChanged;

        if (_advertiser.ActiveConnections is INotifyCollectionChanged advertiserNotify)
            advertiserNotify.CollectionChanged += _advertiserConnectionsChangedHandler;

        if (_discoverer.ActiveConnections is INotifyCollectionChanged discovererNotify)
            discovererNotify.CollectionChanged += _discovererConnectionsChangedHandler;

        RefreshConnectedDevices();
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
        Dispatcher.DispatchAsync(RefreshConnectedDevices);
    }

    void RefreshConnectedDevices()
    {
        var allConnections = _advertiser.ActiveConnections
            .Concat(_discoverer.ActiveConnections)
            .ToList();

        var toRemove = ConnectedDevices
            .Where(vm => !allConnections.Any(c => c.RemoteDevice.Id == vm.Id))
            .ToList();

        foreach (var vm in toRemove)
        {
            vm.IsActive = false;
            ConnectedDevices.Remove(vm);
        }

        foreach (var conn in allConnections.Where(c => !ConnectedDevices.Any(vm => vm.Id == c.RemoteDevice.Id)))
        {
            var vm = _nearbyDeviceViewModelFactory.CreateConnected(conn);
            vm.IsActive = true;
            ConnectedDevices.Add(vm);
        }
    }

    [RelayCommand]
    Task Back()
        => _navigationService.GoBackAsync();
}
