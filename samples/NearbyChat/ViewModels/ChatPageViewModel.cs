using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Indiko.Maui.Controls.Chat.Models;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;
using Plugin.Maui.NearbyConnections.Events;
using Plugin.Maui.NearbyConnections.Models;

namespace NearbyChat.ViewModels;

public partial class ChatPageViewModel : BaseViewModel
{
    readonly IChatMessageService _chatMessageService;
    readonly INearbyConnections _nearbyConnections;

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

    public ChatPageViewModel(IChatMessageService chatMessageService,
        INearbyConnections nearbyConnections)
    {
        ArgumentNullException.ThrowIfNull(chatMessageService);
        ArgumentNullException.ThrowIfNull(nearbyConnections);

        _chatMessageService = chatMessageService;
        _nearbyConnections = nearbyConnections;

        // Subscribe to nearby connections events
        _nearbyConnections.PeerDiscovered += OnPeerDiscovered;
        _nearbyConnections.PeerConnectionChanged += OnPeerConnectionChanged;
        _nearbyConnections.MessageReceived += OnMessageReceived;
    }

    public override async Task OnAppearing(object param)
    {
        var messages = await _chatMessageService.GetMessagesAsync(null, null);
        ChatMessages = new ObservableRangeCollection<ChatMessage>(messages);

        // Refresh peer lists
        RefreshPeerLists();
    }

    [RelayCommand]
    async Task StartAdvertising(CancellationToken cancellationToken)
    {
        var advertiseOptions = new Plugin.Maui.NearbyConnections.Advertise.AdvertisingOptions
        {
            ServiceName = "NearbyChat",
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
    private void ScrolledToLastMessage()
    {
        // mark all existing messages as read
        for (var n = 0; n < ChatMessages.Count; n++)
        {
            if (ChatMessages[n].ReadState == MessageReadState.New)
            {
                ChatMessages[n].ReadState = MessageReadState.Read;
            }
        }
    }

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
}
