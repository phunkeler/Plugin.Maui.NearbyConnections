using NearbyChat.ViewModels;

namespace NearbyChat.Pages;

public partial class LoginPage : BasePage<LoginPageViewModel>
{
    public LoginPage(LoginPageViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }

    private void SfSegmentedControl_SelectionChanged(object sender, Syncfusion.Maui.Toolkit.SegmentedControl.SelectionChangedEventArgs e)
        => Application.Current!.UserAppTheme = e.NewIndex == 0
            ? AppTheme.Light
            : AppTheme.Dark;

    private void OnAvatarSelected(object sender, EventArgs e)
    {
    }
}