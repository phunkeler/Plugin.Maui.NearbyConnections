using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class AdvertisingPageViewModel : BasePageViewModel
{
    readonly INavigationService _navigationService;
    readonly INearby _nearby;
    readonly ILogger<AdvertisingPageViewModel> _logger;

    // The newest request per device, fed by the Requests watch below. Concurrent because the
    // watch writes on a thread-pool thread and the row commands read on the UI thread.
    readonly System.Collections.Concurrent.ConcurrentDictionary<string, NearbyConnectionRequest> _requests
        = new(StringComparer.Ordinal);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleAdvertisingCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsAdvertising { get; set; }

    public ConnectionTracker Connections { get; }

    public NearbyDeviceCollection<AdvertisedDeviceViewModel>? AdvertisedDevices { get; private set; }

    public AdvertisingPageViewModel(
        IDispatcher dispatcher,
        INavigationService navigationService,
        INearby nearby,
        ConnectionTracker connectionTracker,
        ILogger<AdvertisingPageViewModel> logger)
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
        IsAdvertising = nearby.IsAdvertising;
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
        ClearFailure();

        if (!IsAdvertising && !await CanStartAsync(cancellationToken))
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
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The user navigated away or cncelled the operation.
        }
        catch (NearbyException ex)
        {
            LogToggleFailed(_logger, IsAdvertising, ex);

            Fail(
                IsAdvertising
                    ? "Advertising could not be stopped."
                    : "Advertising could not be started.",
                "Check Bluetooth and Wi-Fi are on, then try again.");
        }
        finally
        {
            IsAdvertising = _nearby.IsAdvertising;
            IsBusy = false;
        }
    }

    protected override void NavigatedTo()
    {
        base.NavigatedTo();

        IsAdvertising = _nearby.IsAdvertising;

        // The section 2 request loop: replay hands over the requests already outstanding, then
        // live ones follow. Ending the enumeration on navigation is the only cleanup.
        _ = WatchRequestsAsync(NavigationToken);

        var devices = new NearbyDeviceCollection<AdvertisedDeviceViewModel>(
            _nearby,
            action => Dispatcher.Dispatch(action),
            project: device => new AdvertisedDeviceViewModel(
                device,
                requestFor: id => _requests.TryGetValue(id, out var request) ? request : null,
                _logger),
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

        _requests.Clear();

        if (AdvertisedDevices is { } devices)
        {
            devices.CollectionChanged -= OnAdvertisedDevicesChanged;
            devices.Dispose();

            AdvertisedDevices = null;
            OnPropertyChanged(nameof(AdvertisedDevices));
        }
    }

    async Task WatchRequestsAsync(CancellationToken navigationToken)
    {
        try
        {
            await foreach (var request in _nearby.Requests.WithCancellation(navigationToken))
            {
                _requests[request.RemoteDevice.Id] = request;
            }
        }
        catch (OperationCanceledException) when (navigationToken.IsCancellationRequested)
        {
            // The user navigated away; the enumeration ending is the cleanup.
        }
    }

    void OnAdvertisedDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (AdvertisedDevices is { } devices)
        {
            TrackRelativeTime(devices);
        }
    }

    async Task<bool> CanStartAsync(CancellationToken cancellationToken)
    {
        var permission = await NearbyPermissions.EnsureGrantedAsync();

        if (permission is not PermissionStatus.Granted)
        {
            ReportPermissionDenied(permission);
            return false;
        }

        var availability = await _nearby.CheckAvailabilityAsync(cancellationToken);

        if ((availability & ~NearbyAvailability.WifiDisabled) is not NearbyAvailability.Ready)
        {
            LogUnavailable(_logger, availability);

            Fail("Advertising cannot start yet.", NearbyAvailabilityText.Describe(availability));

            return false;
        }

        return true;
    }

    void ReportPermissionDenied(PermissionStatus status)
        => Fail(
            "Nearby needs permission to advertise.",
            status is PermissionStatus.Denied
                // Denied without a rationale offer means the OS will not prompt again; only settings fixes it.
                ? "Permission was denied for good. Enable it in system settings, then try again."
                : "Tap Start Advertising again and allow the permission.");

    bool CanToggleAdvertising() => !IsBusy;

    [LoggerMessage(
        EventId = 1,
        EventName = nameof(LogToggleFailed),
        Level = LogLevel.Error,
        Message = "Toggling advertising failed. Was advertising: {WasAdvertising}.")]
    static partial void LogToggleFailed(ILogger logger, bool wasAdvertising, Exception exception);

    [LoggerMessage(
        EventId = 2,
        EventName = nameof(LogUnavailable),
        Level = LogLevel.Warning,
        Message = "Advertising was not started: {Availability}.")]
    static partial void LogUnavailable(ILogger logger, NearbyAvailability availability);
}
