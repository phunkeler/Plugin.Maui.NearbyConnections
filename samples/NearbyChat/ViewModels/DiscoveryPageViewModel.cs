using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class DiscoveryPageViewModel : BasePageViewModel
{
    readonly INavigationService _navigationService;
    readonly INearby _session;
    readonly INearbyPermissions _permissions;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleDiscoveryCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsDiscovering { get; set; }

    public IConnectionTracker Connections { get; }

    /// <summary>
    /// Devices in range that are not yet connected — the connectable list.
    /// </summary>
    /// <remarks>
    /// The plugin's own bindable projection, so this page keeps no reconcile loop of its own: the
    /// filter decides which devices appear, and rows are reused across a device's status changes so
    /// a row mid-connect keeps its spinner.
    /// </remarks>
    public NearbyDeviceCollection<DiscoveredDeviceViewModel> DiscoveredDevices { get; }

    public DiscoveryPageViewModel(
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
        IsDiscovering = session.IsDiscovering;

        DiscoveredDevices = new NearbyDeviceCollection<DiscoveredDeviceViewModel>(
            session,
            action => dispatcher.Dispatch(action),
            project: device => new DiscoveredDeviceViewModel(device, session),
            filter: static device => device.Status is NearbyDeviceStatus.Visible or NearbyDeviceStatus.Connecting,
            update: static (row, device) => row.Update(device));

        DiscoveredDevices.CollectionChanged += (_, _) => TrackRelativeTime(DiscoveredDevices);
    }

    [RelayCommand]
    Task Back()
        => _navigationService.GoBackAsync();

    [RelayCommand]
    Task NavigateToConnections()
        => _navigationService.GoToAsync<ConnectionsPageViewModel>();

    [RelayCommand(CanExecute = nameof(CanToggleDiscovery))]
    async Task ToggleDiscovery(CancellationToken cancellationToken)
    {
        if (!IsDiscovering && await _permissions.EnsureGrantedAsync() is not PermissionStatus.Granted)
        {
            return;
        }

        IsBusy = true;

        try
        {
            // Toggles only discovery; advertising is left exactly as the user left it.
            if (IsDiscovering)
            {
                await _session.StopDiscoveryAsync(cancellationToken);
            }
            else
            {
                await _session.StartDiscoveryAsync(cancellationToken);
            }

            IsDiscovering = _session.IsDiscovering;
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected override void NavigatedTo()
    {
        base.NavigatedTo();

        // The collection tracks the session for this view model's whole lifetime, so there is
        // nothing to seed or restart here — devices found while the page was away are already in it.
        IsDiscovering = _session.IsDiscovering;

        TrackRelativeTime(DiscoveredDevices);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DiscoveredDevices.Dispose();
        }

        base.Dispose(disposing);
    }

    bool CanToggleDiscovery() => !IsBusy;
}
