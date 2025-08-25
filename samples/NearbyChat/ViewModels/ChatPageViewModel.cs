using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Indiko.Maui.Controls.Chat.Models;
using NearbyChat.Data;
using NearbyChat.Models;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;
using Plugin.Maui.NearbyConnections.Events;
using Plugin.Maui.NearbyConnections.Models;
using Plugin.Maui.NearbyConnections.Session;

namespace NearbyChat.ViewModels;

public partial class ChatPageViewModel : BaseViewModel
{
    private readonly List<User> _userList;

    readonly AvatarRepository _avatarRepository;
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
        AvatarRepository avatarRepository,
        IChatMessageService chatMessageService,
        INearbyConnections nearbyConnections,
        UserRepository userRepository)
    {
        ArgumentNullException.ThrowIfNull(avatarRepository);
        ArgumentNullException.ThrowIfNull(chatMessageService);
        ArgumentNullException.ThrowIfNull(nearbyConnections);
        ArgumentNullException.ThrowIfNull(userRepository);

        _avatarRepository = avatarRepository;
        _chatMessageService = chatMessageService;
        _nearbyConnections = nearbyConnections;
        _userRepository = userRepository;

        // Subscribe to nearby connections events
        _nearbyConnections.PeerDiscovered += OnPeerDiscovered;
        //_nearbyConnections.PeerConnectionChanged += OnPeerConnectionChanged;
        //_nearbyConnections.MessageReceived += OnMessageReceived;

        var avatars = _avatarRepository.ListAsync().GetAwaiter().GetResult();


        _userList =
        [
            new User
            {
                Avatar = avatars[0],
                DisplayName = "Alex Crowford",
            },
            new User
            {
                Avatar = avatars[1],
                DisplayName = "Sam Maxwell",
            },
            new User
            {
                Avatar = avatars[2],
                DisplayName = "Michael Fitch",
            },
            new User
            {
                Avatar = avatars[3],
                DisplayName = "Mara Mc.Kellogs",
            }
        ];
    }

    [RelayCommand]
    async Task<IAdvertisingSession> StartAdvertising(CancellationToken cancellationToken)
    {
        var advertiseOptions = new Plugin.Maui.NearbyConnections.Advertise.AdvertisingOptions
        {
            DisplayName = "MyDisplayName",
        };

        return await _nearbyConnections.StartAdvertisingAsync(advertiseOptions, cancellationToken);
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
        catch (Exception ex)
        {
            // Handle error - could show a toast or add system message
            Console.WriteLine($"Error sending message: {ex.Message}");
            Console.WriteLine(ex);

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
            var messages = await GetMessagesAsync(DateTime.Now.AddHours(-2), DateTime.Now);
            ChatMessages = new ObservableRangeCollection<ChatMessage>(messages);
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

    /* Testing ChatView sample code directly: */

    public Task<List<ChatMessage>> GetMessagesAsync(DateTime? from, DateTime? until)
    {

        List<ChatMessage> messages = [];

        var timestamp = from ?? DateTime.Now.AddDays(-1);

        string[] messageTemplates =
        [
            "Hey everyone, are we still on for the mountain hike tomorrow?",
            "Yes, I’m all packed up and ready! Can't wait for the adventure.",
            "Same here! I checked the weather forecast; it looks perfect for a hike.",
            "Awesome, what time are we meeting up?",
            "How about 7 AM? We want to make the most of the day.",
            "7 AM sounds great to me. Are we bringing lunch or just snacks?",
            "I think we should pack some sandwiches. It might be a long day.",
            "Good idea! I’ll bring some energy bars and fruit as well.",
            "I’ll take care of the sandwiches then. Any preference for fillings?",
            "Turkey and cheese would be perfect for me, thanks!",
            "I’m good with anything vegetarian if that’s okay.",
            "Got it, one turkey and cheese, and one vegetarian. I’ll sort it out.",
            "By the way, did we finalize the trail we’re taking?",
            "I was thinking we could start with the North Ridge trail, then loop back via the river.",
            "That route sounds amazing! I’ve heard the views are breathtaking.",
            "Yeah, and we’ll get to see the waterfall halfway through.",
            "Perfect! Just make sure to bring your cameras.",
            "Oh, definitely! I’m hoping to capture some wildlife shots too.",
            "Same here, but I hope we don’t run into any bears!",
            "Haha, let's hope not! But I think we’ll be safe if we stick together.",
            "True, and I’m bringing a whistle just in case.",
            "Great thinking! Better safe than sorry.",
            "What about water? How many bottles are you guys bringing?",
            "I’m packing two big bottles and a water filter just in case.",
            "Smart move. I’ll bring a hydration pack and some electrolyte tablets.",
            "Sounds like we’re all set then! Are we carpooling?",
            "Yes, I can drive if you both want to chip in for gas.",
            "Thanks, Alex! I’m in for carpooling and happy to pitch in.",
            "Same here, much appreciated! I’ll bring some road snacks too.",
            "Alright, see you both tomorrow bright and early!",
            "Looking forward to it! Let’s make it an unforgettable hike.",
        ];

        DateTime? lastDateAdded = null;
        var min = 0;
        for (var i = 0; i < messageTemplates.Length; i++)
        {

            var messageReadState = MessageReadState.Read;
            var messageDeliveryState = MessageDeliveryState.Read;

            timestamp = timestamp.AddMinutes(min);

            if (i > 28)
            {
                messageReadState = MessageReadState.New;
                messageDeliveryState = MessageDeliveryState.Sent;
            }
            var actor = _userList[i % 4];

            ChatMessage chatMessage = new()
            {
                TextContent = messageTemplates[i],
                IsOwnMessage = false,
                Timestamp = timestamp,
                MessageId = Guid.NewGuid().ToString(),
                MessageType = MessageType.Text,
                ReadState = messageReadState,
                DeliveryState = messageDeliveryState,
            };


            // Add emoji reactions to specific messages
            if (i == 1)
            {
                chatMessage.Reactions.Add(new ChatMessageReaction
                {
                    Emoji = "😊",
                    Count = 3,
                    ParticipantIds = ["user1", "user2", "user3"]
                });
                chatMessage.Reactions.Add(new ChatMessageReaction
                {
                    Emoji = "❤️",
                    Count = 2,
                    ParticipantIds = ["user4", "user5"]
                });
            }
            else if (i == 2)
            {
                chatMessage.Reactions.Add(new ChatMessageReaction
                {
                    Emoji = "😂",
                    Count = 4,
                    ParticipantIds = new List<string> { "user1", "user2", "user3" }
                });
                chatMessage.Reactions.Add(new ChatMessageReaction
                {
                    Emoji = "❤️",
                    Count = 10,
                    ParticipantIds = new List<string> { "user4", "user5" }
                });
                chatMessage.Reactions.Add(new ChatMessageReaction
                {
                    Emoji = "👍",
                    Count = 4,
                    ParticipantIds = new List<string> { "user2" }
                });
            }
            else if (i == 15)
            {
                chatMessage.Reactions.Add(new ChatMessageReaction
                {
                    Emoji = "👍",
                    Count = 1,
                    ParticipantIds = new List<string> { "user2" }
                });
            }

            // Add replies for specific messages
            if (i == 5)
            {
                chatMessage.ReplyToMessage = new RepliedMessage
                {
                    MessageId = messages[4].MessageId,
                    TextPreview = RepliedMessage.GenerateTextPreview(messages[4].TextContent),
                    SenderId = messages[4].SenderInitials
                };
            }
            else if (i == 11)
            {
                chatMessage.ReplyToMessage = new RepliedMessage
                {
                    MessageId = messages[9].MessageId,
                    TextPreview = RepliedMessage.GenerateTextPreview(messages[9].TextContent),
                    SenderId = messages[9].SenderInitials
                };

                chatMessage.Reactions.Add(new ChatMessageReaction
                {
                    Emoji = "👍",
                    Count = 2,
                    ParticipantIds = new List<string> { "user2", "user3" }
                });
            }

            else if (i == 16)
            {
                chatMessage.Reactions.Add(new ChatMessageReaction
                {
                    Emoji = "😊",
                    Count = 1,
                    ParticipantIds = new List<string> { "user1", }
                });
                chatMessage.Reactions.Add(new ChatMessageReaction
                {
                    Emoji = "❤️",
                    Count = 2,
                    ParticipantIds = new List<string> { "user4", "user5" }
                });
            }

            messages.Add(chatMessage);
            min += 15;
        }

        var systemMessage = new ChatMessage()
        {
            DeliveryState = MessageDeliveryState.Delivered,
            IsOwnMessage = false,
            MessageId = Guid.NewGuid().ToString("N"),
            MessageType = MessageType.System,
            Reactions = [],
            TextContent = "This is a message from the system."
        };

        messages.Insert(15, systemMessage);

        //// insert date separators
        for (int i = 0; i < messages.OrderBy(e => e.Timestamp).ToList().Count; i++)
        {
            var message = messages[i];
            if (lastDateAdded == null || (message?.Timestamp.Date != lastDateAdded))
            {
                messages.Insert(i, new ChatMessage
                {
                    DeliveryState = MessageDeliveryState.Delivered,
                    IsOwnMessage = false,
                    MessageId = Guid.NewGuid().ToString(),
                    MessageType = MessageType.Date,
                    Reactions = [],
                    TextContent = "Test",
                    Timestamp = DateTime.UtcNow
                });
                lastDateAdded = DateTime.UtcNow;
                i++;
            }
        }


        return Task.FromResult(messages);
    }


}
