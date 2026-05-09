using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class AdvertisingPageViewModel : BasePageViewModel
{
    readonly INavigationService _navigationService;
    readonly INearbyAdvertiser _advertiser;
    readonly INearbyDeviceViewModelFactory _nearbyDeviceViewModelFactory;

    IDispatcherTimer? _relativeTimeRefreshTimer;
    NotifyCollectionChangedEventHandler? _pendingRequestsChangedHandler;
    NotifyCollectionChangedEventHandler? _activeConnectionsChangedHandler;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleAdvertisingCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsAdvertising { get; set; }

    [ObservableProperty]
    public partial int ConnectedDevicesCount { get; set; }

    public ObservableCollection<AdvertisedDeviceViewModel> AdvertisedDevices { get; } = [];

    public AdvertisingPageViewModel(
        IDispatcher dispatcher,
        IMessenger messenger,
        INavigationService navigationService,
        INearbyAdvertiser advertiser,
        INearbyDeviceViewModelFactory nearbyDeviceViewModelFactory)
        : base(dispatcher, messenger)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(advertiser);
        ArgumentNullException.ThrowIfNull(nearbyDeviceViewModelFactory);

        _navigationService = navigationService;
        _advertiser = advertiser;
        _nearbyDeviceViewModelFactory = nearbyDeviceViewModelFactory;
    }

    [RelayCommand]
    Task Back()
        => _navigationService.GoBackAsync();

    [RelayCommand]
    Task NavigateToConnections()
        => _navigationService.GoToAsync<ConnectionsPageViewModel>();

    [RelayCommand(CanExecute = nameof(CanToggleAdvertising))]
    async Task ToggleAdvertising(CancellationToken cancellationToken)
    {
        IsBusy = true;

        try
        {
            if (IsAdvertising)
            {
                await _advertiser.StopAsync();
            }
            else
            {
                await _advertiser.StartAsync(cancellationToken);
            }

            IsAdvertising = _advertiser.IsAdvertising;
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected override void NavigatedTo()
    {
        IsAdvertising = _advertiser.IsAdvertising;
        ConnectedDevicesCount = _advertiser.ActiveConnections.Count;

        // Subscribe to collection changes
        _pendingRequestsChangedHandler = OnPendingRequestsChanged;
        _activeConnectionsChangedHandler = OnActiveConnectionsChanged;

        if (_advertiser.PendingRequests is INotifyCollectionChanged pendingNotify)
            pendingNotify.CollectionChanged += _pendingRequestsChangedHandler;

        if (_advertiser.ActiveConnections is INotifyCollectionChanged activeNotify)
            activeNotify.CollectionChanged += _activeConnectionsChangedHandler;

        // Populate initial state
        AdvertisedDevices.Clear();
        foreach (var request in _advertiser.PendingRequests)
        {
            var vm = _nearbyDeviceViewModelFactory.CreateAdvertiser(request);
            vm.IsActive = true;
            AdvertisedDevices.Add(vm);
        }
        UpdateRelativeTimeRefreshTimer();

        base.NavigatedTo();
    }

    protected override void NavigatedFrom()
    {
        if (_advertiser.PendingRequests is INotifyCollectionChanged pendingNotify && _pendingRequestsChangedHandler is not null)
            pendingNotify.CollectionChanged -= _pendingRequestsChangedHandler;

        if (_advertiser.ActiveConnections is INotifyCollectionChanged activeNotify && _activeConnectionsChangedHandler is not null)
            activeNotify.CollectionChanged -= _activeConnectionsChangedHandler;

        _pendingRequestsChangedHandler = null;
        _activeConnectionsChangedHandler = null;

        foreach (var device in AdvertisedDevices)
        {
            device.IsActive = false;
        }

        base.NavigatedFrom();
    }

    void OnPendingRequestsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.DispatchAsync(() =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null)
            {
                foreach (NearbyConnectionRequest request in e.NewItems)
                {
                    if (AdvertisedDevices.Any(d => d.Id == request.RemoteDevice.Id))
                        continue;

                    var vm = _nearbyDeviceViewModelFactory.CreateAdvertiser(request);
                    vm.IsActive = true;
                    AdvertisedDevices.Add(vm);
                    UpdateRelativeTimeRefreshTimer();
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems is not null)
            {
                foreach (NearbyConnectionRequest request in e.OldItems)
                {
                    var vm = AdvertisedDevices.FirstOrDefault(d => d.Id == request.RemoteDevice.Id);
                    if (vm is not null)
                    {
                        vm.IsActive = false;
                        AdvertisedDevices.Remove(vm);
                        UpdateRelativeTimeRefreshTimer();
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var vm in AdvertisedDevices)
                    vm.IsActive = false;
                AdvertisedDevices.Clear();
                UpdateRelativeTimeRefreshTimer();
            }
        });
    }

    void OnActiveConnectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.DispatchAsync(() =>
        {
            ConnectedDevicesCount = _advertiser.ActiveConnections.Count;
        });
    }

    bool CanToggleAdvertising() => !IsBusy;

    void UpdateRelativeTimeRefreshTimer()
    {
        if (AdvertisedDevices.Count >= 1)
        {
            StartRelativeTimeRefreshTimer();
        }
        else
        {
            StopRelativeTimeRefreshTimer();
        }
    }

    void StartRelativeTimeRefreshTimer()
    {
        _relativeTimeRefreshTimer = Dispatcher.CreateTimer();
        _relativeTimeRefreshTimer.Interval = TimeSpan.FromSeconds(30);
        _relativeTimeRefreshTimer.Tick += OnRelativeTimeRefreshTimerTick;
        _relativeTimeRefreshTimer.Start();
    }

    void StopRelativeTimeRefreshTimer()
    {
        _relativeTimeRefreshTimer?.Stop();
        _relativeTimeRefreshTimer?.Tick -= OnRelativeTimeRefreshTimerTick;
        _relativeTimeRefreshTimer = null;
    }

    void OnRelativeTimeRefreshTimerTick(object? sender, EventArgs e)
    {
        foreach (var advertisedDevice in AdvertisedDevices)
        {
            advertisedDevice.RefreshRelativeTime();
        }
    }
}
