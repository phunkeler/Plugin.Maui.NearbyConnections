using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Messages;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class AdvertisingPageViewModel : BasePageViewModel,
    IRecipient<AdvertisingStateChangedMessage>,
    IRecipient<ConnectionRequestMessage>,
    IRecipient<ConnectionResponseMessage>
{
    readonly INavigationService _navigationService;
    readonly INearbyConnectionsService _nearbyConnectionsService;
    IDispatcherTimer? _relativeTimeRefreshTimer;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleAdvertisingCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsAdvertising { get; set; }

    public ObservableCollection<AdvertisedDeviceViewModel> AdvertisedDevices { get; } = [];

    public AdvertisingPageViewModel(
        IMessenger messenger,
        INavigationService navigationService,
        INearbyConnectionsService nearbyConnectionsService)
        : base(messenger)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(nearbyConnectionsService);

        _navigationService = navigationService;
        _nearbyConnectionsService = nearbyConnectionsService;

        IsAdvertising = _nearbyConnectionsService.IsAdvertising;

        foreach (var inbound in _nearbyConnectionsService.Devices.Where(d => d.State == NearbyDeviceState.ConnectionRequestedInbound))
        {
            var vm = new AdvertisedDeviceViewModel(inbound, _nearbyConnectionsService)
            {
                IsActive = true,
            };
            AdvertisedDevices.Add(vm);
            UpdateRelativeTimeRefreshTimer();
        }
    }

    [RelayCommand]
    async Task Back()
    {
        await _navigationService.GoBackAsync();
    }

    [RelayCommand(CanExecute = nameof(CanToggleAdvertising))]
    async Task ToggleAdvertising(CancellationToken cancellationToken)
    {
        IsBusy = true;

        try
        {
            if (IsAdvertising)
            {
                await _nearbyConnectionsService.StopAdvertisingAsync(cancellationToken);
            }
            else
            {
                await _nearbyConnectionsService.StartAdvertisingAsync(cancellationToken);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected override void NavigatedFrom()
    {
        foreach (var device in AdvertisedDevices)
        {
            device.IsActive = false;
        }

        base.NavigatedFrom();
    }

    public void Receive(AdvertisingStateChangedMessage message)
        => IsAdvertising = message.Value;

    public void Receive(ConnectionRequestMessage message)
    {
        if (AdvertisedDevices.Any(d => d.Id == message.Value.Id))
        {
            return;
        }

        var vm = new AdvertisedDeviceViewModel(message.Value, _nearbyConnectionsService)
        {
            IsActive = true,
        };
        AdvertisedDevices.Add(vm);
        UpdateRelativeTimeRefreshTimer();
    }

    public void Receive(ConnectionResponseMessage message)
    {

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
        foreach (var advertisedDevice in AdvertisedDevices)
        {
            advertisedDevice.RefreshRelativeTime();
        }
    }
}
