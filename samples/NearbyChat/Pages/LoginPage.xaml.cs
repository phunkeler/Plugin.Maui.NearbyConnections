using NearbyChat.ViewModels;

namespace NearbyChat.Pages;

public partial class LoginPage : BasePage<LoginPageViewModel>
{
    private readonly Button[] _avatarButtons;
    private Button? _selectedAvatarButton;
    private int _selectedAvatarIndex;


    public LoginPage(LoginPageViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();

        _avatarButtons = [Avatar1, Avatar2, Avatar3, Avatar4, Avatar5, Avatar6];

        // Set initial selected avatar
        SelectAvatar(0);
    }

    private void OnAvatarSelected(object sender, EventArgs e)
    {
        if (sender is Button clickedButton)
        {
            // Find the index of the clicked button
            int index = Array.IndexOf(_avatarButtons, clickedButton);
            if (index >= 0)
            {
                SelectAvatar(index);
            }
        }
    }

    private void SelectAvatar(int index)
    {
        // Reset previous selection
        if (_selectedAvatarButton != null)
        {
            _selectedAvatarButton.Scale = 1.0;
            _selectedAvatarButton.BorderWidth = 0;
        }

        // Set new selection
        _selectedAvatarIndex = index;
        _selectedAvatarButton = _avatarButtons[index];

        // Apply selection styling
        _selectedAvatarButton.Scale = 1.1;
        _selectedAvatarButton.BorderWidth = 4;
        _selectedAvatarButton.BorderColor = Color.FromArgb("#2563eb");
    }

    private void OnUsernameChanged(object sender, TextChangedEventArgs e)
    {
        // Enable/disable login button based on username input
        LoginButton.IsEnabled = !string.IsNullOrWhiteSpace(e.NewTextValue);
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var username = UsernameEntry.Text?.Trim();

        if (string.IsNullOrEmpty(username))
        {
            await DisplayAlert("Error", "Please enter a display name", "OK");
            return;
        }

        // Here you would implement the actual login logic
        // For now, we'll just show the selected values
        char avatarLetter = (char)('A' + _selectedAvatarIndex);

        await DisplayAlert("Login Info",
            $"Username: {username}\nAvatar: {avatarLetter}",
            "OK");

        // TODO: Implement actual NearbyConnections login logic
        // This is where you would:
        // 1. Initialize the Plugin.Maui.NearbyConnections
        // 2. Start advertising/discovering
        // 3. Navigate to the chat screen
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Start the pulsing animation for the status indicator
        StartStatusAnimation();
    }

    private async void StartStatusAnimation()
    {
        while (true)
        {
            await StatusIndicator.FadeTo(0.3, 1000);
            await StatusIndicator.FadeTo(1.0, 1000);
        }
    }

    private void UsernameEntry_TextChanged(object sender, TextChangedEventArgs e)
    {
    }
}