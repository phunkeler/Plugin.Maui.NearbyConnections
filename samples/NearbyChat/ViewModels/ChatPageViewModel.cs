using System.Collections.ObjectModel;
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

    [ObservableProperty]
    ObservableCollection<INearbyDevice> _nearbyDevices = [];

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
            var advertiseOptions = new AdvertisingOptions
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
    async Task SelectDevice(INearbyDevice? device)
    {
        if (device is null)
            return;

        await _nearbyConnections.SendInvitation(device, CancellationToken.None);
    }

    [RelayCommand]
    async Task AcceptInvitation(INearbyDevice? device)
    {
        if (device is null)
            return;

        await Task.CompletedTask;
    }

    [RelayCommand]
    async Task RejectInvitation(INearbyDevice? device)
    {
        if (device is null)
            return;
        await Task.CompletedTask;
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
                    _ = HandleNearbyEvent(eventData);
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

    async Task HandleNearbyEvent(object eventData)
    {
        // Handle different event types
        switch (eventData)
        {
            case NearbyDeviceFound foundEvent:
                AddOrUpdateNearbyDevice(foundEvent.Device);
                break;

            case NearbyDeviceLost lostEvent:
                RemoveNearbyDevice(lostEvent.Device.Id);
                break;

            case InvitationReceived invitationEvent:
                AddOrUpdateNearbyDevice(invitationEvent.From);

                break;

            case InvitationAnswered answeredEvent:

                break;

            case NearbyDeviceDisconnected disconnectedEvent:
                RemoveNearbyDevice(disconnectedEvent.Device.Id);
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

    void AddOrUpdateNearbyDevice(INearbyDevice device)
    {
        if (device is null)
        {
            return;
        }

        // assume caller may be on background thread; ensure UI thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var existing = NearbyDevices.FirstOrDefault(d => d.Id == device.Id);

            if (existing is null)
            {
                NearbyDevices.Add(device);
            }
            else if (!ReferenceEquals(existing, device))
            {
                // replace the item so CollectionView can refresh the item template
                var index = NearbyDevices.IndexOf(existing);
                if (index >= 0)
                    NearbyDevices[index] = device;
            }
        });
    }

    void RemoveNearbyDevice(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var existing = NearbyDevices.FirstOrDefault(d => d.Id == id);
            if (existing != null)
                NearbyDevices.Remove(existing);
        });
    }

    public void Dispose()
    {
        _eventSubscription?.Dispose();

        GC.SuppressFinalize(this);
    }
}
