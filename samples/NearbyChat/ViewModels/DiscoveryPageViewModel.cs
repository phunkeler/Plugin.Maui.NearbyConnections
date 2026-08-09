using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
    readonly RelativeTimeTicker _relativeTimeTicker;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleDiscoveryCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsDiscovering { get; set; }

    public IConnectionTracker Connections { get; }

    /// <summary>
    /// Devices in range that are not yet connected — the connectable list.
    /// </summary>
    public ObservableCollection<DiscoveredDeviceViewModel> DiscoveredDevices { get; } = [];

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
        _relativeTimeTicker = new RelativeTimeTicker(dispatcher, TimeSpan.FromSeconds(30), OnRelativeTimeRefreshTimerTick);
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

        DiscoveredDevices.Clear();
        IsDiscovering = _session.IsDiscovering;

        // Devices is the state, so this page projects it rather than accumulating its own events.
        // Devices found while the page was away are therefore already present.
        RegisterSessionSubscription(
            () => ((INotifyCollectionChanged)_session.Devices).CollectionChanged += OnDevicesChanged,
            () => ((INotifyCollectionChanged)_session.Devices).CollectionChanged -= OnDevicesChanged);

        RegisterSessionSubscription(
            () => _session.ConnectionEstablished += OnConnectionChanged,
            () => _session.ConnectionEstablished -= OnConnectionChanged);

        RegisterSessionSubscription(
            () => _session.ConnectionDropped += OnConnectionChanged,
            () => _session.ConnectionDropped -= OnConnectionChanged);

        Rebuild();
    }

    protected override void NavigatedFrom()
    {
        base.NavigatedFrom();

        _relativeTimeTicker.SetActive(false);
    }

    void OnDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => Rebuild();

    /// <summary>
    /// A device that connects leaves the connectable list; one that drops rejoins it.
    /// </summary>
    void OnConnectionChanged(object? sender, NearbyConnectionChangedEventArgs e)
        => Rebuild();

    /// <summary>
    /// Reconciles the list against the session, preserving existing row instances so bindings and
    /// per-row state survive.
    /// </summary>
    void Rebuild()
    {
        var connectable = _session.Devices
            .Where(d => d.Status is NearbyDeviceStatus.Visible or NearbyDeviceStatus.Connecting)
            .ToList();

        for (var i = DiscoveredDevices.Count - 1; i >= 0; i--)
        {
            if (!connectable.Any(d => d.Id == DiscoveredDevices[i].Id))
            {
                DiscoveredDevices.RemoveAt(i);
            }
        }

        foreach (var device in connectable)
        {
            if (!DiscoveredDevices.Any(d => d.Id == device.Id))
            {
                DiscoveredDevices.Add(new DiscoveredDeviceViewModel(device, _session));
            }
        }

        UpdateRelativeTimeRefreshTimer();
    }

    bool CanToggleDiscovery() => !IsBusy;

    void UpdateRelativeTimeRefreshTimer()
        => _relativeTimeTicker.SetActive(DiscoveredDevices.Count >= 1);

    void OnRelativeTimeRefreshTimerTick()
    {
        foreach (var discoveredDevice in DiscoveredDevices)
        {
            discoveredDevice.RefreshRelativeTime();
        }
    }
}
