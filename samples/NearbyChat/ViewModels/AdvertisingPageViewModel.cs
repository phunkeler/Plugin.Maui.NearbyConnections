using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class AdvertisingPageViewModel : BasePageViewModel, IAdvertiserHandler
{
    readonly INavigationService _navigationService;
    readonly INearbyAdvertiser _advertiser;
    readonly INearbyDeviceViewModelFactory _nearbyDeviceViewModelFactory;

    IDispatcherTimer? _relativeTimeRefreshTimer;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleAdvertisingCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsAdvertising { get; set; }

    [ObservableProperty]
    public partial int ConnectedDevicesCount { get; set; }

    public ObservableCollection<AdvertisedDeviceViewModel> AdvertisedDevices { get; } = [];

    IDispatcher? IAdvertiserHandler.Dispatcher => Dispatcher;

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
                await _advertiser.StartAsync();
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
        base.NavigatedTo();

        AdvertisedDevices.Clear();
        IsAdvertising = _advertiser.IsAdvertising;
        _ = _advertiser.EventsAsync(NavigationToken).RunAsync(this);
    }

    protected override void NavigatedFrom()
    {
        foreach (var device in AdvertisedDevices)
        {
            device.IsActive = false;
        }

        base.NavigatedFrom();
    }

    Task IAdvertiserHandler.OnConnectionRequested(AdvertiserEvent.ConnectionRequested ev)
    {
        if (AdvertisedDevices.Any(d => d.Id == ev.Request.RemoteDevice.Id))
        {
            return Task.CompletedTask;
        }

        var vm = _nearbyDeviceViewModelFactory.CreateAdvertiser(ev.Request);
        vm.IsActive = true;
        AdvertisedDevices.Add(vm);
        UpdateRelativeTimeRefreshTimer();
        return Task.CompletedTask;
    }

    Task IAdvertiserHandler.OnConnectionAccepted(AdvertiserEvent.ConnectionAccepted ev)
    {
        ConnectedDevicesCount++;
        return Task.CompletedTask;
    }

    Task IAdvertiserHandler.OnConnectionDropped(AdvertiserEvent.ConnectionDropped ev)
    {
        var vm = AdvertisedDevices.FirstOrDefault(d => d.Id == ev.Connection.RemoteDevice.Id);
        if (vm is not null)
        {
            vm.IsActive = false;
            AdvertisedDevices.Remove(vm);
            ConnectedDevicesCount--;
            UpdateRelativeTimeRefreshTimer();
        }
        return Task.CompletedTask;
    }

    Task IAdvertiserHandler.OnConnectionRequestExpired(AdvertiserEvent.ConnectionRequestExpired ev)
    {
        var vm = AdvertisedDevices.FirstOrDefault(d => d.Id == ev.Request.RemoteDevice.Id);
        if (vm is not null)
        {
            vm.IsActive = false;
            AdvertisedDevices.Remove(vm);
            UpdateRelativeTimeRefreshTimer();
        }
        return Task.CompletedTask;
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
