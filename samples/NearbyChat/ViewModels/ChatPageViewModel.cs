using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Indiko.Maui.Controls.Chat.Models;
using NearbyChat.Services;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class ChatPageViewModel : BaseViewModel
{
    readonly IChatMessageService _chatMessageService;
    readonly INearbyConnections _nearbyConnections;

    [ObservableProperty]
    ObservableRangeCollection<ChatMessage> _chatMessages = [];

    public ChatPageViewModel(IChatMessageService chatMessageService,
        INearbyConnections nearbyConnections)
    {
        ArgumentNullException.ThrowIfNull(chatMessageService);
        ArgumentNullException.ThrowIfNull(nearbyConnections);

        _chatMessageService = chatMessageService;
        _nearbyConnections = nearbyConnections;
    }

    public override async Task OnAppearing(object param)
    {
        var messages = await _chatMessageService.GetMessagesAsync(null, null);
        ChatMessages = new ObservableRangeCollection<ChatMessage>(messages);

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
}
