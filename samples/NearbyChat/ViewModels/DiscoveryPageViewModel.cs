using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using Plugin.Maui.NearbyDevices;

namespace NearbyChat.ViewModels;

public partial class DiscoveryPageViewModel : BasePageViewModel, IDiscovererHandler
{
    readonly INavigationService _navigationService;
    readonly INearbyDiscoverer _discoverer;
    readonly INearbyPermissions _permissions;
    readonly RelativeTimeTicker _relativeTimeTicker;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleDiscoveryCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsDiscovering { get; set; }

    public IConnectionTracker Connections { get; }

    public ObservableCollection<DiscoveredDeviceViewModel> DiscoveredDevices { get; } = [];

    IDispatcher? IDiscovererHandler.Dispatcher => Dispatcher;

    public DiscoveryPageViewModel(
        IDispatcher dispatcher,
        INavigationService navigationService,
        INearbyDiscoverer discoverer,
        IConnectionTracker connectionTracker,
        INearbyPermissions permissions)
        : base(dispatcher)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(discoverer);
        ArgumentNullException.ThrowIfNull(connectionTracker);
        ArgumentNullException.ThrowIfNull(permissions);

        _navigationService = navigationService;
        _discoverer = discoverer;
        Connections = connectionTracker;
        _permissions = permissions;
        _relativeTimeTicker = new RelativeTimeTicker(dispatcher, TimeSpan.FromSeconds(30), OnRelativeTimeRefreshTimerTick);
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
        if (!IsDiscovering && !await _permissions.EnsureGrantedAsync())
        {
            return;
        }

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
        base.NavigatedFrom();

        _relativeTimeTicker.SetActive(false);
    }

    Task IDiscovererHandler.OnDeviceFound(DiscovererEvent.DeviceFound ev)
    {
        if (DiscoveredDevices.Any(d => d.Id == ev.Device.Id))
        {
            return Task.CompletedTask;
        }

        var vm = new DiscoveredDeviceViewModel(ev.Device, _discoverer);
        DiscoveredDevices.Add(vm);
        UpdateRelativeTimeRefreshTimer();
        return Task.CompletedTask;
    }

    Task IDiscovererHandler.OnDeviceLost(DiscovererEvent.DeviceLost ev)
    {
        var vm = DiscoveredDevices.FirstOrDefault(d => d.Id == ev.Device.Id);
        if (vm is not null)
        {
            DiscoveredDevices.Remove(vm);
            UpdateRelativeTimeRefreshTimer();
        }
        return Task.CompletedTask;
    }

    Task IDiscovererHandler.OnDeviceConnected(DiscovererEvent.DeviceConnected ev)
    {
        var vm = DiscoveredDevices.FirstOrDefault(d => d.Id == ev.Connection.RemoteDevice.Id);
        if (vm is not null)
        {
            DiscoveredDevices.Remove(vm);
            UpdateRelativeTimeRefreshTimer();
        }
        return Task.CompletedTask;
    }

    Task IDiscovererHandler.OnDeviceDisconnected(DiscovererEvent.DeviceDisconnected ev)
    {
        var vm = DiscoveredDevices.FirstOrDefault(d => d.Id == ev.Connection.RemoteDevice.Id);
        if (vm is not null)
        {
            DiscoveredDevices.Remove(vm);
            UpdateRelativeTimeRefreshTimer();
        }
        return Task.CompletedTask;
    }

    bool CanToggleDiscovery() => !IsBusy;

    void UpdateRelativeTimeRefreshTimer()
        => _relativeTimeTicker.SetActive(DiscoveredDevices.Count >= 1);

    void OnRelativeTimeRefreshTimerTick()
    {
        foreach (var discoveredDevice in DiscoveredDevices)
        {
            discoveredDevice.RefreshRelativeTime();
        }
    }
}
