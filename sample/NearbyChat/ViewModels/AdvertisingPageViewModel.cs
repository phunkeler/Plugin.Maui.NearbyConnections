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
    IRecipient<ConnectionResponseMessage>,
    IRecipient<DeviceLostMessage>
{
    readonly INavigationService _navigationService;
    readonly INearbyConnectionsService _nearbyConnectionsService;
    readonly INearbyDeviceViewModelFactory _nearbyDeviceViewModelFactory;

    IDispatcherTimer? _relativeTimeRefreshTimer;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleAdvertisingCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsAdvertising { get; set; }

    public ObservableCollection<AdvertisedDeviceViewModel> AdvertisedDevices { get; } = [];

    public AdvertisingPageViewModel(
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

        IsAdvertising = _nearbyConnectionsService.IsAdvertising;

        foreach (var inbound in _nearbyConnectionsService.Devices.Where(d => d.State == NearbyDeviceState.ConnectionRequestedInbound))
        {
            var vm = _nearbyDeviceViewModelFactory.CreateAdvertiser(inbound);
            vm.IsActive = true;
            AdvertisedDevices.Add(vm);
            UpdateRelativeTimeRefreshTimer();
        }
    }

    [RelayCommand]
    Task Back()
        => _navigationService.GoBackAsync();

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

    public async void Receive(AdvertisingStateChangedMessage message)
        => await Dispatcher.DispatchAsync(() => IsAdvertising = message.Value);

    public async void Receive(ConnectionRequestMessage message)
        => await Dispatcher.DispatchAsync(() =>
         {
             if (AdvertisedDevices.Any(d => d.Id == message.Value.Id))
             {
                 return;
             }

             var vm = _nearbyDeviceViewModelFactory.CreateAdvertiser(message.Value);
             vm.IsActive = true;
             AdvertisedDevices.Add(vm);
             UpdateRelativeTimeRefreshTimer();
         });

    public async void Receive(DeviceLostMessage message)
        => await Dispatcher.DispatchAsync(() =>
        {
            var device = AdvertisedDevices.FirstOrDefault(d => d.Id == message.Value.Id);

            if (device is not null)
            {
                device.IsActive = false;
                AdvertisedDevices.Remove(device);
                UpdateRelativeTimeRefreshTimer();
            }
        });

    public async void Receive(ConnectionResponseMessage message)
        => await Dispatcher.DispatchAsync(() =>
        {

        });

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
