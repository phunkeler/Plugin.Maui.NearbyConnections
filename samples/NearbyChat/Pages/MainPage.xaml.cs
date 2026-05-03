using NearbyChat.ViewModels;

namespace NearbyChat.Pages;

public partial class MainPage : BasePage<MainPageViewModel>
{
    public MainPage(MainPageViewModel viewModel)
        : base(viewModel)
    {
        InitializeComponent();
    }
}