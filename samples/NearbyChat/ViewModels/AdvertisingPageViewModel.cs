using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class AdvertisingPageViewModel : BasePageViewModel
{
    readonly INavigationService _navigationService;
    readonly INearby _session;
    readonly INearbyPermissions _permissions;
    readonly RelativeTimeTicker _relativeTimeTicker;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleAdvertisingCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsAdvertising { get; set; }

    public IConnectionTracker Connections { get; }

    /// <summary>
    /// Devices awaiting a response to their inbound connection request.
    /// </summary>
    public ObservableCollection<AdvertisedDeviceViewModel> AdvertisedDevices { get; } = [];

    public AdvertisingPageViewModel(
        IDispatcher dispatcher,
        INavigationService navigationService,
        INearby session,
        IConnectionTracker connectionTracker,
        INearbyPermissions permissions)
        : base(dispatcher)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(connectionTracker);
        ArgumentNullException.ThrowIfNull(permissions);

        _navigationService = navigationService;
        _session = session;
        Connections = connectionTracker;
        _permissions = permissions;
        _relativeTimeTicker = new RelativeTimeTicker(dispatcher, TimeSpan.FromSeconds(30), OnRelativeTimeRefreshTimerTick);
        IsAdvertising = session.IsAdvertising;
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
        if (!IsAdvertising && await _permissions.EnsureGrantedAsync() is not PermissionStatus.Granted)
        {
            return;
        }

        IsBusy = true;

        try
        {
            // Advertising and discovery are independent: this toggles only advertising and leaves
            // discovery exactly as the user left it on the other page.
            if (IsAdvertising)
            {
                await _session.StopAdvertisingAsync(cancellationToken);
            }
            else
            {
                await _session.StartAdvertisingAsync(cancellationToken);
            }

            IsAdvertising = _session.IsAdvertising;
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
        IsAdvertising = _session.IsAdvertising;

        // Requests that arrived while the page was away are already in Devices. Seeding first and
        // then watching is what replaces the old seed-plus-subscribe pair: the stream has no
        // replay, so the current state has to come from the collection.
        foreach (var device in _session.Devices.Where(d => d.Status is NearbyDeviceStatus.RequestReceived))
        {
            AddDevice(device);
        }

        _ = WatchDevicesAsync(NavigationToken);
    }

    /// <summary>
    /// Adds a row when a device starts asking for a connection and removes it once it stops —
    /// answered, expired, or gone. Ends when the page is navigated away from.
    /// </summary>
    async Task WatchDevicesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var change in _session.Devices.Changes.WithCancellation(cancellationToken))
            {
                var device = change.Device;

                var isPending = change.Action is not NearbyDeviceChangeAction.Removed
                    && device.Status is NearbyDeviceStatus.RequestReceived;

                // Changes arrive on a platform background thread; AdvertisedDevices is bound.
                await Dispatcher.DispatchAsync(() =>
                {
                    if (isPending)
                    {
                        AddDevice(device);
                    }
                    else
                    {
                        RemoveDevice(device.Id);
                    }
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Navigated away.
        }
    }

    protected override void NavigatedFrom()
    {
        base.NavigatedFrom();

        _relativeTimeTicker.SetActive(false);
    }

    void AddDevice(NearbyDevice device)
    {
        // A device is a snapshot, so an existing row is handed the newer one rather than left
        // showing the instance it was created with.
        if (AdvertisedDevices.FirstOrDefault(d => d.Id == device.Id) is { } existing)
        {
            existing.Update(device);
            return;
        }

        AdvertisedDevices.Add(new AdvertisedDeviceViewModel(device, _session));
        UpdateRelativeTimeRefreshTimer();
    }

    void RemoveDevice(string deviceId)
    {
        if (AdvertisedDevices.FirstOrDefault(d => d.Id == deviceId) is not { } vm)
        {
            return;
        }

        AdvertisedDevices.Remove(vm);
        UpdateRelativeTimeRefreshTimer();
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
