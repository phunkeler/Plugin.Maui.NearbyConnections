using NearbyChat.ViewModels;

namespace NearbyChat.Pages;

public partial class ChatPage : BasePage<ChatPageViewModel>
{
    public ChatPage(ChatPageViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }
}