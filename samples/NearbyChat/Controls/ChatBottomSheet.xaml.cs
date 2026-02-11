using NearbyChat.ViewModels;
using Plugin.Maui.BottomSheet;

namespace NearbyChat.Controls;

public partial class ChatBottomSheet : BottomSheet
{
    public ChatBottomSheet(ChatViewModel viewModel)
    {
        BindingContext = viewModel;
        InitializeComponent();
    }
}