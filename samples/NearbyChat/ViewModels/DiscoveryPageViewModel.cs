using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class DiscoveryPageViewModel : BasePageViewModel
{
    readonly INavigationService _navigationService;
    readonly INearby _session;
    readonly ILogger<DiscoveryPageViewModel> _logger;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleDiscoveryCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsDiscovering { get; set; }

    public ConnectionTracker Connections { get; }

    public NearbyDeviceCollection<DiscoveredDeviceViewModel>? DiscoveredDevices { get; private set; }

    public DiscoveryPageViewModel(
        IDispatcher dispatcher,
        INavigationService navigationService,
        INearby session,
        ConnectionTracker connectionTracker,
        ILogger<DiscoveryPageViewModel> logger)
        : base(dispatcher)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(connectionTracker);
        ArgumentNullException.ThrowIfNull(logger);

        _navigationService = navigationService;
        _session = session;
        _logger = logger;
        Connections = connectionTracker;
        IsDiscovering = session.IsDiscovering;
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
        if (!IsDiscovering && await NearbyPermissions.EnsureGrantedAsync() is not PermissionStatus.Granted)
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

        IsDiscovering = _session.IsDiscovering;

        // Seeding is free: the constructor reads the current device set before it starts watching,
        // so devices found while the page was away are already in the new collection.
        var devices = new NearbyDeviceCollection<DiscoveredDeviceViewModel>(
            _session,
            action => Dispatcher.Dispatch(action),
            project: device => new DiscoveredDeviceViewModel(device, _session, _logger),
            filter: static device => device.Status is NearbyDeviceStatus.Visible or NearbyDeviceStatus.Connecting,
            update: static (row, device) => row.Update(device));

        devices.CollectionChanged += OnDiscoveredDevicesChanged;

        DiscoveredDevices = devices;
        OnPropertyChanged(nameof(DiscoveredDevices));

        TrackRelativeTime(devices);
    }

    protected override void NavigatedFrom()
    {
        base.NavigatedFrom();

        if (DiscoveredDevices is { } devices)
        {
            devices.CollectionChanged -= OnDiscoveredDevicesChanged;
            devices.Dispose();

            DiscoveredDevices = null;
            OnPropertyChanged(nameof(DiscoveredDevices));
        }
    }

    void OnDiscoveredDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DiscoveredDevices is { } devices)
        {
            TrackRelativeTime(devices);
        }
    }

    bool CanToggleDiscovery() => !IsBusy;
}
