using NearbyChat.ViewModels;

namespace NearbyChat.Pages;

public partial class MainPage : BasePage<MainPageViewModel>
{
    public MainPage(MainPageViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }

    private async void OnChatClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//ChatPage");
    }
}