using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class AdvertisingPageViewModel : BasePageViewModel
{
    readonly INavigationService _navigationService;
    readonly INearbyConnections _session;
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
        INearbyConnections session,
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

        // Never `+=` directly: the session is a singleton, so an un-detached handler would leak this
        // ViewModel and fire twice after the second visit.
        RegisterSessionSubscription(
            () => _session.ConnectionRequested += OnConnectionRequested,
            () => _session.ConnectionRequested -= OnConnectionRequested);

        RegisterSessionSubscription(
            () => _session.ConnectionEstablished += OnConnectionChanged,
            () => _session.ConnectionEstablished -= OnConnectionChanged);

        RegisterSessionSubscription(
            () => _session.ConnectionDropped += OnConnectionChanged,
            () => _session.ConnectionDropped -= OnConnectionChanged);

        // Requests that arrived while the page was away are already in Devices.
        foreach (var device in _session.Devices.Where(d => d.Status is NearbyDeviceStatus.RequestReceived))
        {
            AddDevice(device);
        }
    }

    protected override void NavigatedFrom()
    {
        base.NavigatedFrom();

        _relativeTimeTicker.SetActive(false);
    }

    void OnConnectionRequested(object? sender, NearbyConnectionRequestedEventArgs e)
        => AddDevice(e.Device);

    /// <summary>
    /// A pending request leaves this list once it becomes a connection or the device goes away —
    /// established and dropped are the same removal from this page's point of view.
    /// </summary>
    void OnConnectionChanged(object? sender, NearbyConnectionChangedEventArgs e)
        => RemoveDevice(e.Device.Id);

    void AddDevice(NearbyDevice device)
    {
        if (AdvertisedDevices.Any(d => d.Id == device.Id))
        {
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
