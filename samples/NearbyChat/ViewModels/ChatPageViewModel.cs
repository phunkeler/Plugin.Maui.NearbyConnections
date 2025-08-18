using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Indiko.Maui.Controls.Chat.Models;
using NearbyChat.Services;

namespace NearbyChat.ViewModels;

public partial class ChatPageViewModel : BaseViewModel
{
    readonly IChatMessageService _chatMessageService;

    [ObservableProperty]
    ObservableRangeCollection<ChatMessage> _chatMessages = [];

    public ChatPageViewModel(IChatMessageService chatMessageService)
    {
        ArgumentNullException.ThrowIfNull(chatMessageService);

        _chatMessageService = chatMessageService;
    }

    public override async Task OnAppearing(object param)
    {
        var messages = await _chatMessageService.GetMessagesAsync(null, null);
        ChatMessages = new ObservableRangeCollection<ChatMessage>(messages);

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
