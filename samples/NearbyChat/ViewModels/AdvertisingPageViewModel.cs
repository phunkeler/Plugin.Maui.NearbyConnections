using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class AdvertisingPageViewModel : BasePageViewModel
{
    readonly INavigationService _navigationService;
    readonly INearby _nearby;

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
    /// The plugin's own bindable projection, so this page keeps no add/remove bookkeeping of its
    /// own: a device that stops asking — answered, expired, or gone — stops matching the filter and
    /// its row is dropped.
    /// </remarks>
    public NearbyDeviceCollection<AdvertisedDeviceViewModel> AdvertisedDevices { get; }

    public AdvertisingPageViewModel(
        IDispatcher dispatcher,
        INavigationService navigationService,
        INearby nearby,
        ConnectionTracker connectionTracker)
        : base(dispatcher)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(nearby);
        ArgumentNullException.ThrowIfNull(connectionTracker);

        _navigationService = navigationService;
        _nearby = nearby;
        Connections = connectionTracker;
        IsAdvertising = nearby.IsAdvertising;

        AdvertisedDevices = new NearbyDeviceCollection<AdvertisedDeviceViewModel>(
            nearby,
            action => dispatcher.Dispatch(action),
            project: device => new AdvertisedDeviceViewModel(device, nearby),
            filter: static device => device.Status is NearbyDeviceStatus.RequestReceived,
            update: static (row, device) => row.Update(device));

        AdvertisedDevices.CollectionChanged += (_, _) => TrackRelativeTime(AdvertisedDevices);
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
                await _nearby.StopAdvertisingAsync(cancellationToken);
            }
            else
            {
                await _nearby.StartAdvertisingAsync(cancellationToken);
            }

            IsAdvertising = _nearby.IsAdvertising;
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected override void NavigatedTo()
    {
        base.NavigatedTo();
        IsAdvertising = _nearby.IsAdvertising;
        TrackRelativeTime(AdvertisedDevices);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            AdvertisedDevices.Dispose();
        }

        base.Dispose(disposing);
    }

    bool CanToggleAdvertising() => !IsBusy;
}
