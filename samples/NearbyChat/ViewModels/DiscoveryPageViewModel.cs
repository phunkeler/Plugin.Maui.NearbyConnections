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
    IDispatcherTimer? _relativeTimeRefreshTimer;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleDiscoveryCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsDiscovering { get; set; }

    public ObservableCollection<DiscoveredDeviceViewModel> DiscoveredDevices { get; } = [];

    public DiscoveryPageViewModel(
        IMessenger messenger,
        INavigationService navigationService,
        INearbyConnectionsService nearbyConnectionsService)
        : base(messenger)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(nearbyConnectionsService);

        _navigationService = navigationService;
        _nearbyConnectionsService = nearbyConnectionsService;

        IsDiscovering = _nearbyConnectionsService.IsDiscovering;

        foreach (var discovered in _nearbyConnectionsService.DiscoveredDevices)
        {
            DiscoveredDevices.Add(new DiscoveredDeviceViewModel(discovered, _nearbyConnectionsService));
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

    public void Receive(DiscoveringStateChangedMessage message)
        => IsDiscovering = message.Value;

    public void Receive(DeviceFoundMessage message)
    {
        if (DiscoveredDevices.Any(d => d.Id == message.Value.Id))
            return;

        var discoveredDevice = new DiscoveredDevice(message.Value, message.FoundAt);
        DiscoveredDevices.Add(new DiscoveredDeviceViewModel(discoveredDevice, _nearbyConnectionsService));
        UpdateRelativeTimeRefreshTimer();
    }

    public void Receive(DeviceLostMessage message)
    {
        var device = DiscoveredDevices.FirstOrDefault(d => d.Id == message.Value.Id);
        if (device is not null)
        {
            DiscoveredDevices.Remove(device);
            UpdateRelativeTimeRefreshTimer();
        }
    }

    bool CanToggleDiscovery() => !IsBusy;

    void UpdateRelativeTimeRefreshTimer()
    {
        if (DiscoveredDevices.Count >= 1)
            StartRelativeTimeRefreshTimer();
        else
            StopRelativeTimeRefreshTimer();
    }

    void StartRelativeTimeRefreshTimer()
    {
        _relativeTimeRefreshTimer = Application.Current!.Dispatcher.CreateTimer();
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
        foreach (var device in DiscoveredDevices)
        {
            device.RefreshRelativeTime();
        }
    }
}