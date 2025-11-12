using NearbyChat.ViewModels;

namespace NearbyChat.Pages;

public partial class LoginPage : BasePage<LoginPageViewModel>
{
    public LoginPage(LoginPageViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }
}