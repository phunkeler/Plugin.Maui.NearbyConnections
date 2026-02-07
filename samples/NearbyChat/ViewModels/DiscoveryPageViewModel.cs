using Plugin.Maui.NearbyConnections;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Messages;
using NearbyChat.Services;

namespace NearbyChat.ViewModels;

public partial class DiscoveryPageViewModel : BasePageViewModel,
    IRecipient<DiscoveringStateChangedMessage>,
    IRecipient<DeviceFoundMessage>,
    IRecipient<DeviceLostMessage>
{
    readonly INavigationService _navigationService;
    readonly INearbyConnectionsService _nearbyConnectionsService;
    readonly INearbyDeviceViewModelFactory _nearbyDeviceViewModelFactory;

    IDispatcherTimer? _relativeTimeRefreshTimer;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleDiscoveryCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsDiscovering { get; set; }

    public ObservableCollection<DiscoveredDeviceViewModel> DiscoveredDevices { get; } = [];

    public DiscoveryPageViewModel(
        IDispatcher dispatcher,
        IMessenger messenger,
        INavigationService navigationService,
        INearbyConnectionsService nearbyConnectionsService,
        INearbyDeviceViewModelFactory nearbyDeviceViewModelFactory)
        : base(dispatcher, messenger)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(nearbyConnectionsService);
        ArgumentNullException.ThrowIfNull(nearbyDeviceViewModelFactory);

        _navigationService = navigationService;
        _nearbyConnectionsService = nearbyConnectionsService;
        _nearbyDeviceViewModelFactory = nearbyDeviceViewModelFactory;

        IsDiscovering = _nearbyConnectionsService.IsDiscovering;

        foreach (var discovered in _nearbyConnectionsService.Devices.Where(d => d.State == NearbyDeviceState.Discovered))
        {
            var vm = _nearbyDeviceViewModelFactory.CreateDiscoverer(discovered);
            vm.IsActive = true;
            DiscoveredDevices.Add(vm);
            UpdateRelativeTimeRefreshTimer();
        }
    }

    [RelayCommand]
    async Task Back()
    {
        await _navigationService.GoBackAsync();
    }

    [RelayCommand(CanExecute = nameof(CanToggleDiscovery))]
    async Task ToggleDiscovery(CancellationToken cancellationToken)
    {
        IsBusy = true;

        try
        {
            if (IsDiscovering)
            {
                await _nearbyConnectionsService.StopDiscoveryAsync(cancellationToken);
            }
            else
            {
                await _nearbyConnectionsService.StartDiscoveryAsync(cancellationToken);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected override void NavigatedFrom()
    {
        foreach (var device in DiscoveredDevices)
        {
            device.IsActive = false;
        }

        base.NavigatedFrom();
    }

    public async void Receive(DiscoveringStateChangedMessage message)
        => await Dispatcher.DispatchAsync(() => IsDiscovering = message.Value);

    public async void Receive(DeviceFoundMessage message)
        => await Dispatcher.DispatchAsync(() =>
        {
            if (DiscoveredDevices.Any(d => d.Id == message.Value.Id))
            {
                return;
            }

            var vm = _nearbyDeviceViewModelFactory.CreateDiscoverer(message.Value);
            vm.IsActive = true;
            DiscoveredDevices.Add(vm);
            UpdateRelativeTimeRefreshTimer();
        });

    public async void Receive(DeviceLostMessage message)
        => await Dispatcher.DispatchAsync(() =>
        {
            var device = DiscoveredDevices.FirstOrDefault(d => d.Id == message.Value.Id);
            if (device is not null)
            {
                device.IsActive = false;
                DiscoveredDevices.Remove(device);
                UpdateRelativeTimeRefreshTimer();
            }
        });

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