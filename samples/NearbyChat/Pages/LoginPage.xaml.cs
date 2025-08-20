using NearbyChat.ViewModels;

namespace NearbyChat.Pages;

public partial class LoginPage : BasePage<LoginPageViewModel>
{
    public LoginPage(LoginPageViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }

    private void OnUsernameChanged(object sender, TextChangedEventArgs e)
    {
        // Enable/disable login button based on username input
        LoginButton.IsEnabled = !string.IsNullOrWhiteSpace(e.NewTextValue);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
    }


    private void UsernameEntry_TextChanged(object sender, TextChangedEventArgs e)
    {
    }

    private void SfSegmentedControl_SelectionChanged(object sender, Syncfusion.Maui.Toolkit.SegmentedControl.SelectionChangedEventArgs e)
        => Application.Current!.UserAppTheme = e.NewIndex == 0
            ? AppTheme.Light
            : AppTheme.Dark;
}