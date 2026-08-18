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
    readonly INearby _nearby;
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
        INearby nearby,
        ConnectionTracker connectionTracker,
        ILogger<DiscoveryPageViewModel> logger)
        : base(dispatcher)
    {
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(nearby);
        ArgumentNullException.ThrowIfNull(connectionTracker);
        ArgumentNullException.ThrowIfNull(logger);

        _navigationService = navigationService;
        _nearby = nearby;
        _logger = logger;
        Connections = connectionTracker;
        IsDiscovering = nearby.IsDiscovering;
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
        ClearFailure();

        // Only the start path is gated. Stopping needs neither permission nor a working radio, and
        // refusing to stop because the user revoked something mid-session would strand the toggle on.
        if (!IsDiscovering && !await CanStartAsync(cancellationToken))
        {
            return;
        }

        IsBusy = true;

        try
        {
            // Toggles only discovery; advertising is left exactly as the user left it.
            if (IsDiscovering)
            {
                await _nearby.StopDiscoveryAsync(cancellationToken);
            }
            else
            {
                await _nearby.StartDiscoveryAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The user navigated away mid-toggle. Nothing to report.
        }
        catch (NearbyException ex)
        {
            LogToggleFailed(_logger, IsDiscovering, ex);

            Fail(
                IsDiscovering
                    ? "Discovery could not be stopped."
                    : "Discovery could not be started.",
                "Check Bluetooth and Wi-Fi are on, then try again.");
        }
        finally
        {
            // Read the session rather than assuming the toggle took: after a failure the flag has to
            // match what the session actually did, or the button label lies about the current state.
            IsDiscovering = _nearby.IsDiscovering;
            IsBusy = false;
        }
    }

    protected override void NavigatedTo()
    {
        base.NavigatedTo();

        IsDiscovering = _nearby.IsDiscovering;

        // Seeding is free: the constructor reads the current device set before it starts watching,
        // so devices found while the page was away are already in the new collection.
        var devices = new NearbyDeviceCollection<DiscoveredDeviceViewModel>(
            _nearby,
            action => Dispatcher.Dispatch(action),
            project: device => new DiscoveredDeviceViewModel(device, _nearby, _logger),
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

    /// <summary>
    /// Whether the platform can start discovery right now: permissions held and the radios
    /// available. Records the reason and returns <see langword="false"/> when it cannot.
    /// </summary>
    async Task<bool> CanStartAsync(CancellationToken cancellationToken)
    {
        var permission = await NearbyPermissions.EnsureGrantedAsync();
        if (permission is not PermissionStatus.Granted)
        {
            ReportPermissionDenied(permission);
            return false;
        }

        // The plugin documents this as the call to make before starting: without it a disabled radio
        // fails silently on Android and simply discovers nothing on iOS.
        var availability = await _nearby.CheckAvailabilityAsync(cancellationToken);

        // WifiDisabled alone is a warning, not a blocker — Bluetooth alone still carries a
        // connection, just slowly — so it is the one flag that does not stop a start.
        if ((availability & ~NearbyAvailability.WifiDisabled) is not NearbyAvailability.Ready)
        {
            LogUnavailable(_logger, availability);

            Fail("Discovery cannot start yet.", NearbyAvailabilityText.Describe(availability));

            return false;
        }

        return true;
    }

    void ReportPermissionDenied(PermissionStatus status)
        => Fail(
            "Nearby needs permission to find devices.",
            status is PermissionStatus.Denied
                // Denied without a rationale offer means the OS will not prompt again; only settings fixes it.
                ? "Permission was denied for good. Enable it in system settings, then try again."
                : "Tap Start Discovery again and allow the permission.");

    bool CanToggleDiscovery() => !IsBusy;

    [LoggerMessage(
        EventId = 1,
        EventName = nameof(LogToggleFailed),
        Level = LogLevel.Error,
        Message = "Toggling discovery failed. Was discovering: {WasDiscovering}.")]
    static partial void LogToggleFailed(ILogger logger, bool wasDiscovering, Exception exception);

    [LoggerMessage(
        EventId = 2,
        EventName = nameof(LogUnavailable),
        Level = LogLevel.Warning,
        Message = "Discovery was not started: {Availability}.")]
    static partial void LogUnavailable(ILogger logger, NearbyAvailability availability);
}
