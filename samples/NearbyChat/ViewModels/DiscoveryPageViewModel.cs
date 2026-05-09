using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class DiscoveryPageViewModel : BasePageViewModel
{
    readonly INavigationService _navigationService;
    readonly INearbyDiscoverer _discoverer;
    readonly INearbyDeviceViewModelFactory _nearbyDeviceViewModelFactory;

    IDispatcherTimer? _relativeTimeRefreshTimer;
    NotifyCollectionChangedEventHandler? _nearbyDevicesChangedHandler;
    NotifyCollectionChangedEventHandler? _activeConnectionsChangedHandler;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleDiscoveryCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsDiscovering { get; set; }

    [ObservableProperty]
    public partial int ConnectedDevicesCount { get; set; }

    public ObservableCollection<DiscoveredDeviceViewModel> DiscoveredDevices { get; } = [];

    public DiscoveryPageViewModel(
        IDispatcher dispatcher,
        IMessenger messenger,
        INavigationService navigationService,
        INearbyDiscoverer discoverer,
        INearbyDeviceViewModelFactory nearbyDeviceViewModelFactory)
        : base(dispatcher, messenger)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(discoverer);
        ArgumentNullException.ThrowIfNull(nearbyDeviceViewModelFactory);

        _navigationService = navigationService;
        _discoverer = discoverer;
        _nearbyDeviceViewModelFactory = nearbyDeviceViewModelFactory;
    }

    [RelayCommand]
    Task Back()
        => _navigationService.GoBackAsync();

    [RelayCommand]
    Task NavigateToConnections()
        => _navigationService.GoToAsync<ConnectionsPageViewModel>();

    [RelayCommand(CanExecute = nameof(CanToggleDiscovery))]
    async Task ToggleDiscovery(CancellationToken cancellationToken)
    {
        IsBusy = true;

        try
        {
            if (IsDiscovering)
            {
                await _discoverer.StopAsync();
            }
            else
            {
                await _discoverer.StartAsync(cancellationToken);
            }

            IsDiscovering = _discoverer.IsDiscovering;
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected override void NavigatedTo()
    {
        IsDiscovering = _discoverer.IsDiscovering;
        ConnectedDevicesCount = _discoverer.ActiveConnections.Count;

        _nearbyDevicesChangedHandler = OnNearbyDevicesChanged;
        _activeConnectionsChangedHandler = OnActiveConnectionsChanged;

        if (_discoverer.NearbyDevices is INotifyCollectionChanged nearbyNotify)
            nearbyNotify.CollectionChanged += _nearbyDevicesChangedHandler;

        if (_discoverer.ActiveConnections is INotifyCollectionChanged activeNotify)
            activeNotify.CollectionChanged += _activeConnectionsChangedHandler;

        DiscoveredDevices.Clear();
        foreach (var device in _discoverer.NearbyDevices)
        {
            var vm = _nearbyDeviceViewModelFactory.CreateDiscoverer(device);
            vm.IsActive = true;
            DiscoveredDevices.Add(vm);
        }
        UpdateRelativeTimeRefreshTimer();

        base.NavigatedTo();
    }

    protected override void NavigatedFrom()
    {
        if (_discoverer.NearbyDevices is INotifyCollectionChanged nearbyNotify && _nearbyDevicesChangedHandler is not null)
            nearbyNotify.CollectionChanged -= _nearbyDevicesChangedHandler;

        if (_discoverer.ActiveConnections is INotifyCollectionChanged activeNotify && _activeConnectionsChangedHandler is not null)
            activeNotify.CollectionChanged -= _activeConnectionsChangedHandler;

        _nearbyDevicesChangedHandler = null;
        _activeConnectionsChangedHandler = null;

        foreach (var device in DiscoveredDevices)
        {
            device.IsActive = false;
        }

        base.NavigatedFrom();
    }

    void OnNearbyDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.DispatchAsync(() =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null)
            {
                foreach (NearbyDevice device in e.NewItems)
                {
                    if (DiscoveredDevices.Any(d => d.Id == device.Id))
                        continue;

                    var vm = _nearbyDeviceViewModelFactory.CreateDiscoverer(device);
                    vm.IsActive = true;
                    DiscoveredDevices.Add(vm);
                    UpdateRelativeTimeRefreshTimer();
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems is not null)
            {
                foreach (NearbyDevice device in e.OldItems)
                {
                    var vm = DiscoveredDevices.FirstOrDefault(d => d.Id == device.Id);
                    if (vm is not null)
                    {
                        vm.IsActive = false;
                        DiscoveredDevices.Remove(vm);
                        UpdateRelativeTimeRefreshTimer();
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var vm in DiscoveredDevices)
                    vm.IsActive = false;
                DiscoveredDevices.Clear();
                UpdateRelativeTimeRefreshTimer();
            }
        });
    }

    void OnActiveConnectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.DispatchAsync(() =>
        {
            ConnectedDevicesCount = _discoverer.ActiveConnections.Count;
        });
    }

    bool CanToggleDiscovery() => !IsBusy;

    void UpdateRelativeTimeRefreshTimer()
    {
        if (DiscoveredDevices.Count >= 1)
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
        foreach (var discoveredDevice in DiscoveredDevices)
        {
            discoveredDevice.RefreshRelativeTime();
        }
    }
}
