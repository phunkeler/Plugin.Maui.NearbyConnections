using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NearbyChat.Data;
using NearbyChat.Models;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class ChatPageViewModel : BaseViewModel, IDisposable
{
    readonly AvatarRepository _avatarRepository;
    readonly INearbyConnections _nearbyConnections;
    readonly IUserService _userService;

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
    ObservableCollection<NearbyDevice> _nearbyDevices = [];

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
            // Set display name before advertising
            _nearbyConnections.DisplayName = CurrentUser?.DisplayName ?? DeviceInfo.Current.Name;

            await _nearbyConnections.StartAdvertisingAsync(cancellationToken);
        }
        else
        {
            await _nearbyConnections.StopAdvertisingAsync(cancellationToken);
        }
    }

    [RelayCommand]
    async Task Discover(ToggledEventArgs toggledEventArgs, CancellationToken cancellationToken)
    {
        if (toggledEventArgs.Value)
        {
            await _nearbyConnections.StartDiscoveryAsync(cancellationToken);
        }
        else
        {
            await _nearbyConnections.StopDiscoveryAsync(cancellationToken);
        }
    }

    [RelayCommand]
    async Task SelectDevice(NearbyDevice? device)
    {
        if (device is null)
            return;

        await _nearbyConnections.SendInvitationAsync(device, CancellationToken.None);
    }

    [RelayCommand]
    async Task AcceptInvitation(NearbyDevice? device)
    {
        if (device is null)
            return;

        await _nearbyConnections.AcceptInvitationAsync(device, CancellationToken.None);
    }

    [RelayCommand]
    async Task RejectInvitation(NearbyDevice? device)
    {
        if (device is null)
            return;

        await _nearbyConnections.DeclineInvitationAsync(device);
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

        SubscribeToNearbyConnectionsEvents();
    }

    [RelayCommand]
    void Disappearing()
    {
        UnsubscribeFromNearbyConnectionsEvents();
    }

    void SubscribeToNearbyConnectionsEvents()
    {
        _nearbyConnections.Events.DeviceFound += (s, e) => AddOrUpdateNearbyDevice(e.NearbyDevice);
        _nearbyConnections.Events.DeviceLost += (s, e) => RemoveNearbyDevice(e.NearbyDevice.Id);
        _nearbyConnections.Events.DeviceDisconnected += (s, e) => RemoveNearbyDevice(e.NearbyDevice.Id);
        _nearbyConnections.Events.ConnectionRequested += (s, e) => AddOrUpdateNearbyDevice(e.NearbyDevice);
        _nearbyConnections.Events.ConnectionResponded += (s, e) => AddOrUpdateNearbyDevice(e.NearbyDevice);
    }

    void UnsubscribeFromNearbyConnectionsEvents()
    {
        _nearbyConnections.Events.DeviceFound -= (s, e) => AddOrUpdateNearbyDevice(e.NearbyDevice);
        _nearbyConnections.Events.DeviceLost -= (s, e) => RemoveNearbyDevice(e.NearbyDevice.Id);
        _nearbyConnections.Events.DeviceDisconnected -= (s, e) => RemoveNearbyDevice(e.NearbyDevice.Id);
        _nearbyConnections.Events.ConnectionRequested -= (s, e) => AddOrUpdateNearbyDevice(e.NearbyDevice);
        _nearbyConnections.Events.ConnectionResponded -= (s, e) => AddOrUpdateNearbyDevice(e.NearbyDevice);
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

    void AddOrUpdateNearbyDevice(NearbyDevice device)
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
            else if (existing.Equals(device))
            {
                var index = NearbyDevices.Remove(existing);
                NearbyDevices.Add(existing);
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
        GC.SuppressFinalize(this);
    }
}
