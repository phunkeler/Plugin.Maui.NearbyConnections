using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class DiscoveryPageViewModel : BasePageViewModel, IDiscovererHandler
{
    readonly INavigationService _navigationService;
    readonly INearbyDiscoverer _discoverer;
    readonly INearbyDeviceViewModelFactory _nearbyDeviceViewModelFactory;

    IDispatcherTimer? _relativeTimeRefreshTimer;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleDiscoveryCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsDiscovering { get; set; }

    [ObservableProperty]
    public partial int ConnectedDevicesCount { get; set; }

    public ObservableCollection<DiscoveredDeviceViewModel> DiscoveredDevices { get; } = [];

    IDispatcher? IDiscovererHandler.Dispatcher => Dispatcher;

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
                await _discoverer.StartAsync();
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
        base.NavigatedTo();

        DiscoveredDevices.Clear();
        IsDiscovering = _discoverer.IsDiscovering;
        _ = _discoverer.EventsAsync(NavigationToken).RunAsync(this);
    }

    protected override void NavigatedFrom()
    {
        foreach (var device in DiscoveredDevices)
        {
            device.IsActive = false;
        }

        base.NavigatedFrom();
    }

    Task IDiscovererHandler.OnDeviceFound(DiscovererEvent.DeviceFound ev)
    {
        if (DiscoveredDevices.Any(d => d.Id == ev.Device.Id))
        {
            return Task.CompletedTask;
        }

        var vm = _nearbyDeviceViewModelFactory.CreateDiscoverer(ev.Device);
        vm.IsActive = true;
        DiscoveredDevices.Add(vm);
        UpdateRelativeTimeRefreshTimer();
        return Task.CompletedTask;
    }

    Task IDiscovererHandler.OnDeviceLost(DiscovererEvent.DeviceLost ev)
    {
        var vm = DiscoveredDevices.FirstOrDefault(d => d.Id == ev.Device.Id);
        if (vm is not null)
        {
            vm.IsActive = false;
            DiscoveredDevices.Remove(vm);
            UpdateRelativeTimeRefreshTimer();
        }
        return Task.CompletedTask;
    }

    Task IDiscovererHandler.OnDeviceConnected(DiscovererEvent.DeviceConnected ev)
    {
        ConnectedDevicesCount++;

        var vm = DiscoveredDevices.FirstOrDefault(d => d.Id == ev.Connection.RemoteDevice.Id);
        if (vm is not null)
        {
            vm.IsActive = false;
            DiscoveredDevices.Remove(vm);
            UpdateRelativeTimeRefreshTimer();
        }
        return Task.CompletedTask;
    }

    Task IDiscovererHandler.OnDeviceDisconnected(DiscovererEvent.DeviceDisconnected ev)
    {
        ConnectedDevicesCount--;

        var vm = DiscoveredDevices.FirstOrDefault(d => d.Id == ev.Connection.RemoteDevice.Id);
        if (vm is not null)
        {
            vm.IsActive = false;
            DiscoveredDevices.Remove(vm);
            UpdateRelativeTimeRefreshTimer();
        }
        return Task.CompletedTask;
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
