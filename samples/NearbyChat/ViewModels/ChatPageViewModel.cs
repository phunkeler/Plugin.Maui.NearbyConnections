using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NearbyChat.Data;
using NearbyChat.Models;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;
using Plugin.Maui.NearbyConnections.Advertise;
using Plugin.Maui.NearbyConnections.Events;

namespace NearbyChat.ViewModels;

public partial class ChatPageViewModel : BaseViewModel, IDisposable
{
    readonly AvatarRepository _avatarRepository;
    readonly INearbyConnections _nearbyConnections;
    readonly IUserService _userService;

    IDisposable? _eventSubscription;

    [ObservableProperty]
    User? _currentUser;

    [ObservableProperty]
    bool _isRefreshing;

    [ObservableProperty]
    string _connectionStatus = "Not Connected";

    [ObservableProperty]
    bool _isConnected = false;

    [ObservableProperty]
    string _currentMessage = "";

    public ChatPageViewModel(
        AvatarRepository avatarRepository,
        INearbyConnections nearbyConnections,
        IUserService userService)
    {
        ArgumentNullException.ThrowIfNull(avatarRepository);
        ArgumentNullException.ThrowIfNull(nearbyConnections);
        ArgumentNullException.ThrowIfNull(userService);

        _avatarRepository = avatarRepository;
        _nearbyConnections = nearbyConnections;
        _userService = userService;
    }

    [RelayCommand]
    async Task Advertise(ToggledEventArgs toggledEventArgs, CancellationToken cancellationToken)
    {
        if (toggledEventArgs.Value)
        {
            var advertiseOptions = new AdvertiseOptions
            {
                DisplayName = CurrentUser?.DisplayName ?? DeviceInfo.Current.Name,
            };

            await _nearbyConnections.StartAdvertisingAsync(advertiseOptions, cancellationToken);
        }
        else
        {
            await _nearbyConnections.StopAdvertisingAsync();
        }
    }

    [RelayCommand]
    async Task Discover(ToggledEventArgs toggledEventArgs, CancellationToken cancellationToken)
    {
        if (toggledEventArgs.Value)
        {
            var discoveryOptions = new Plugin.Maui.NearbyConnections.Discover.DiscoverOptions
            {
#if ANDROID
                Activity = Platform.CurrentActivity
#endif
            };

            await _nearbyConnections.StartDiscoveryAsync(discoveryOptions, cancellationToken);
        }
        else
        {
            await _nearbyConnections.StopDiscoveryAsync();
        }
    }

    [RelayCommand]
    async Task Refresh(CancellationToken cancellationToken)
    {
        try
        {
            IsRefreshing = true;
            await LoadData(cancellationToken);
        }
        catch
        {
            // Handle exceptions, e.g., show an error message
            Console.WriteLine("Failed to refresh avatars.");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    async Task Appearing(CancellationToken cancellationToken)
    {
        await Refresh(cancellationToken);

        SubscribeToNearbyConnectionsEventChannel();
    }

    [RelayCommand]
    void Disappearing()
    {
        StopEventConsumption();
    }

    private void SubscribeToNearbyConnectionsEventChannel()
    {
        _eventSubscription?.Dispose();

        _eventSubscription = _nearbyConnections.Events
            .Subscribe(
                onNext: eventData =>
                {
                    MainThread.BeginInvokeOnMainThread(() => HandleNearbyEvent(eventData));
                },
                onError: ex =>
                {
                    Console.WriteLine($"Event consumption error: {ex}");
                },
                onCompleted: () =>
                {
                    Console.WriteLine("Event stream completed");
                });
    }

    private void StopEventConsumption()
    {
        _eventSubscription?.Dispose();
        _eventSubscription = null;
    }

    private void HandleNearbyEvent(object eventData)
    {
        // Handle different event types
        switch (eventData)
        {
            case NearbyDeviceFound foundEvent:

                break;

            case NearbyDeviceLost lostEvent:

                break;

            case InvitationReceived invitationEvent:

                break;

            case InvitationAnswered answeredEvent:

                break;

            case NearbyDeviceDisconnected disconnectedEvent:

                break;
        }
    }


    private async Task LoadData(CancellationToken cancellationToken = default)
    {
        try
        {
            IsBusy = true;
            CurrentUser = await _userService.GetActiveUserAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Handle exceptions, e.g., log the error or show a message
            Console.WriteLine($"Error loading avatars: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Dispose()
    {
        _eventSubscription?.Dispose();

        GC.SuppressFinalize(this);
    }
}
