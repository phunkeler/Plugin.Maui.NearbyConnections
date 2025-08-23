using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Indiko.Maui.Controls.Chat.Models;
using NearbyChat.Data;
using NearbyChat.Models;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;
using Plugin.Maui.NearbyConnections.Events;
using Plugin.Maui.NearbyConnections.Models;

namespace NearbyChat.ViewModels;

public partial class ChatPageViewModel : BaseViewModel
{
    readonly IChatMessageService _chatMessageService;
    readonly INearbyConnections _nearbyConnections;
    readonly UserRepository _userRepository;

    [ObservableProperty]
    User? _currentUser;

    [ObservableProperty]
    bool _isRefreshing;

    [ObservableProperty]
    ObservableRangeCollection<ChatMessage> _chatMessages = [];

    [ObservableProperty]
    ObservableRangeCollection<PeerDevice> _discoveredPeers = [];

    [ObservableProperty]
    ObservableRangeCollection<PeerDevice> _connectedPeers = [];

    [ObservableProperty]
    string _connectionStatus = "Not Connected";

    [ObservableProperty]
    bool _isConnected = false;

    [ObservableProperty]
    string _currentMessage = "";

    public ChatPageViewModel(
        IChatMessageService chatMessageService,
        INearbyConnections nearbyConnections,
        UserRepository userRepository)
    {
        ArgumentNullException.ThrowIfNull(chatMessageService);
        ArgumentNullException.ThrowIfNull(nearbyConnections);
        ArgumentNullException.ThrowIfNull(userRepository);

        _chatMessageService = chatMessageService;
        _nearbyConnections = nearbyConnections;
        _userRepository = userRepository;

        // Subscribe to nearby connections events
        _nearbyConnections.PeerDiscovered += OnPeerDiscovered;
        //_nearbyConnections.PeerConnectionChanged += OnPeerConnectionChanged;
        //_nearbyConnections.MessageReceived += OnMessageReceived;
    }

    [RelayCommand]
    async Task StartAdvertising(CancellationToken cancellationToken)
    {
        var advertiseOptions = new Plugin.Maui.NearbyConnections.Advertise.AdvertisingOptions
        {
            DisplayName = "MyDisplayName",
        };

        await _nearbyConnections.StartAdvertisingAsync(advertiseOptions, cancellationToken);
    }

    [RelayCommand]
    async Task StopAdvertising(CancellationToken cancellationToken)
    {
        await _nearbyConnections.StopAdvertisingAsync(cancellationToken);
    }

    [RelayCommand]
    async Task StartDiscovery(CancellationToken cancellationToken)
    {
        var discoveryOptions = new Plugin.Maui.NearbyConnections.Discover.DiscoveringOptions
        {
            ServiceName = "NearbyChat",
        };

        await _nearbyConnections.StartDiscoveryAsync(discoveryOptions, cancellationToken);
    }

    [RelayCommand]
    async Task StopDiscovery(CancellationToken cancellationToken)
    {
        await _nearbyConnections.StopDiscoveryAsync(cancellationToken);
    }

    [RelayCommand]
    async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(CurrentMessage))
            return;

        try
        {
            // Send message to connected peers
            await _nearbyConnections.SendMessageAsync(CurrentMessage);

            // Add message to local chat
            var message = new ChatMessage();

            ChatMessages.Add(message);
            CurrentMessage = "";
        }
        catch (Exception)
        {
            // Handle error - could show a toast or add system message
            var errorMessage = new ChatMessage();
            ChatMessages.Add(errorMessage);
        }
    }

    [RelayCommand]
    async Task ConnectToPeer(PeerDevice peer)
    {
        try
        {
            await _nearbyConnections.ConnectToPeerAsync(peer.Id);
        }
        catch (Exception)
        {
            // Handle connection error
            var errorMessage = new ChatMessage();
            ChatMessages.Add(errorMessage);
        }
    }

    [RelayCommand]
    async Task Refresh()
    {
        try
        {
            IsRefreshing = true;
            await LoadData();
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
    async Task Appearing()
        => await Refresh();

    private void OnPeerDiscovered(object? sender, PeerDiscoveredEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var existingPeer = DiscoveredPeers.FirstOrDefault(p => p.Id == e.Peer.Id);
            if (existingPeer is null)
            {
                DiscoveredPeers.Add(e.Peer);
            }
        });
    }

    private void OnPeerConnectionChanged(object? sender, PeerConnectionChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RefreshPeerLists();
            UpdateConnectionStatus();

            // Add system message about connection change
            var message = new ChatMessage();
            ChatMessages.Add(message);
        });
    }

    private void OnMessageReceived(object? sender, PeerMessageReceivedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var message = new ChatMessage();
            ChatMessages.Add(message);
        });
    }

    private void RefreshPeerLists()
    {
        var discovered = _nearbyConnections.GetDiscoveredPeers();
        var connected = _nearbyConnections.GetConnectedPeers();

        DiscoveredPeers.Clear();
        DiscoveredPeers.AddRange(discovered);

        ConnectedPeers.Clear();
        ConnectedPeers.AddRange(connected);
    }

    private void UpdateConnectionStatus()
    {
        var connectedCount = ConnectedPeers.Count;
        IsConnected = connectedCount > 0;
        ConnectionStatus = connectedCount switch
        {
            0 => "Not Connected",
            1 => "Connected to 1 peer",
            _ => $"Connected to {connectedCount} peers"
        };
    }

    private static string GetConnectionStatusText(PeerConnectionState state) => state switch
    {
        PeerConnectionState.Connecting => "is connecting...",
        PeerConnectionState.Connected => "connected",
        PeerConnectionState.Disconnecting => "is disconnecting...",
        PeerConnectionState.NotConnected => "disconnected",
        _ => "unknown status"
    };

    private async Task LoadData()
    {
        try
        {
            IsBusy = true;
            CurrentUser = await _userRepository.GetCurrentUserAsync();
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
}
