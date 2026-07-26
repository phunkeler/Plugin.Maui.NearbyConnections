using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using Plugin.Maui.NearbyDevices;

namespace NearbyChat.ViewModels;

public partial class AdvertisingPageViewModel : BasePageViewModel, IAdvertiserHandler
{
    readonly INavigationService _navigationService;
    readonly INearbyAdvertiser _advertiser;
    readonly INearbyPermissions _permissions;
    readonly RelativeTimeTicker _relativeTimeTicker;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleAdvertisingCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsAdvertising { get; set; }

    public IConnectionTracker Connections { get; }

    public ObservableCollection<AdvertisedDeviceViewModel> AdvertisedDevices { get; } = [];

    IDispatcher? IAdvertiserHandler.Dispatcher => Dispatcher;

    public AdvertisingPageViewModel(
        IDispatcher dispatcher,
        INavigationService navigationService,
        INearbyAdvertiser advertiser,
        IConnectionTracker connectionTracker,
        INearbyPermissions permissions)
        : base(dispatcher)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(advertiser);
        ArgumentNullException.ThrowIfNull(connectionTracker);
        ArgumentNullException.ThrowIfNull(permissions);

        _navigationService = navigationService;
        _advertiser = advertiser;
        Connections = connectionTracker;
        _permissions = permissions;
        _relativeTimeTicker = new RelativeTimeTicker(dispatcher, TimeSpan.FromSeconds(30), OnRelativeTimeRefreshTimerTick);
        IsAdvertising = advertiser.IsAdvertising;
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
        if (!IsAdvertising && !await _permissions.EnsureGrantedAsync())
        {
            return;
        }

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
        base.NavigatedFrom();

        _relativeTimeTicker.SetActive(false);
    }

    Task IAdvertiserHandler.OnConnectionRequested(AdvertiserEvent.ConnectionRequested ev)
    {
        if (AdvertisedDevices.Any(d => d.Id == ev.Request.RemoteDevice.Id))
        {
            return Task.CompletedTask;
        }

        var vm = new AdvertisedDeviceViewModel(ev.Request, _advertiser);
        AdvertisedDevices.Add(vm);
        UpdateRelativeTimeRefreshTimer();
        return Task.CompletedTask;
    }

    Task IAdvertiserHandler.OnConnectionAccepted(AdvertiserEvent.ConnectionAccepted ev)
    {
        var vm = AdvertisedDevices.FirstOrDefault(d => d.Id == ev.Connection.RemoteDevice.Id);
        if (vm is not null)
        {
            AdvertisedDevices.Remove(vm);
            UpdateRelativeTimeRefreshTimer();
        }
        return Task.CompletedTask;
    }

    Task IAdvertiserHandler.OnConnectionDropped(AdvertiserEvent.ConnectionDropped ev)
    {
        var vm = AdvertisedDevices.FirstOrDefault(d => d.Id == ev.Connection.RemoteDevice.Id);
        if (vm is not null)
        {
            AdvertisedDevices.Remove(vm);
            UpdateRelativeTimeRefreshTimer();
        }
        return Task.CompletedTask;
    }

    Task IAdvertiserHandler.OnConnectionRequestExpired(AdvertiserEvent.ConnectionRequestExpired ev)
    {
        var vm = AdvertisedDevices.FirstOrDefault(d => d.Id == ev.Request.RemoteDevice.Id);
        if (vm is not null)
        {
            AdvertisedDevices.Remove(vm);
            UpdateRelativeTimeRefreshTimer();
        }
        return Task.CompletedTask;
    }

    bool CanToggleAdvertising() => !IsBusy;

    void UpdateRelativeTimeRefreshTimer()
        => _relativeTimeTicker.SetActive(AdvertisedDevices.Count >= 1);

    void OnRelativeTimeRefreshTimerTick()
    {
        foreach (var advertisedDevice in AdvertisedDevices)
        {
            advertisedDevice.RefreshRelativeTime();
        }
    }
}
