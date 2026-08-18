using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class AdvertisingPageViewModel : BasePageViewModel
{
    readonly INavigationService _navigationService;
    readonly INearby _session;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleAdvertisingCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsAdvertising { get; set; }

    public ConnectionTracker Connections { get; }

    /// <summary>
    /// Devices awaiting a response to their inbound connection request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The plugin's own bindable projection, so this page keeps no add/remove bookkeeping of its
    /// own: a device that stops asking — answered, expired, or gone — stops matching the filter and
    /// its row is dropped.
    /// </para>
    /// <para>
    /// Built in <see cref="NavigatedTo"/> and disposed in <see cref="NavigatedFrom"/>, so it lives
    /// exactly as long as the page is on screen. Rebuilding costs one pass over
    /// <see cref="INearby.Devices"/>, which already holds every request that arrived while the page
    /// was away.
    /// </para>
    /// </remarks>
    public NearbyDeviceCollection<AdvertisedDeviceViewModel>? AdvertisedDevices { get; private set; }

    public AdvertisingPageViewModel(
        IDispatcher dispatcher,
        INavigationService navigationService,
        INearby session,
        ConnectionTracker connectionTracker)
        : base(dispatcher)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(connectionTracker);

        _navigationService = navigationService;
        _session = session;
        Connections = connectionTracker;
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
        if (!IsAdvertising && await NearbyPermissions.EnsureGrantedAsync() is not PermissionStatus.Granted)
        {
            return;
        }

        IsBusy = true;

        try
        {
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

        IsAdvertising = _session.IsAdvertising;

        var devices = new NearbyDeviceCollection<AdvertisedDeviceViewModel>(
            _session,
            action => Dispatcher.Dispatch(action),
            project: device => new AdvertisedDeviceViewModel(device, _session),
            filter: static device => device.Status is NearbyDeviceStatus.RequestReceived,
            update: static (row, device) => row.Update(device));

        devices.CollectionChanged += OnAdvertisedDevicesChanged;

        AdvertisedDevices = devices;
        OnPropertyChanged(nameof(AdvertisedDevices));

        TrackRelativeTime(devices);
    }

    protected override void NavigatedFrom()
    {
        base.NavigatedFrom();

        if (AdvertisedDevices is { } devices)
        {
            devices.CollectionChanged -= OnAdvertisedDevicesChanged;
            devices.Dispose();

            AdvertisedDevices = null;
            OnPropertyChanged(nameof(AdvertisedDevices));
        }
    }

    void OnAdvertisedDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (AdvertisedDevices is { } devices)
        {
            TrackRelativeTime(devices);
        }
    }

    bool CanToggleAdvertising() => !IsBusy;
}
