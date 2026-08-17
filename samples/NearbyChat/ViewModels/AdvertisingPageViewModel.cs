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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleAdvertisingCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsAdvertising { get; set; }

    public IConnectionTracker Connections { get; }

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
        IsAdvertising = session.IsAdvertising;

        AdvertisedDevices = new NearbyDeviceCollection<AdvertisedDeviceViewModel>(
            session,
            action => dispatcher.Dispatch(action),
            project: device => new AdvertisedDeviceViewModel(device, session),
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

        // The collection tracks the session for this view model's whole lifetime, so requests that
        // arrived while the page was away are already in it — nothing to seed here.
        IsAdvertising = _session.IsAdvertising;

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
