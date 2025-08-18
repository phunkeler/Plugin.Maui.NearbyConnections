using System.Collections.ObjectModel;
using System.Globalization;
using NearbyChat.ViewModels;

namespace NearbyChat.Pages;

public partial class ChatPage : BasePage<ChatPageViewModel>
{
    public ObservableCollection<ChatMessage> Messages { get; set; } = new();
    private bool _isConnected;
    private string _peerName = "";

    public ChatPage(ChatPageViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
        UpdateConnectionStatus(false, "");
    }

    private async void OnAdvertiseClicked(object sender, EventArgs e)
    {
        try
        {
            AdvertiseButton.IsEnabled = false;
            AdvertiseButton.Text = "Advertising...";

            // TODO: Implement actual advertising logic using the plugin
            // await NearbyConnections.Current.StartAdvertisingAsync();

            await DisplayAlert("Info", "Advertising started. Waiting for connections...", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to start advertising: {ex.Message}", "OK");
        }
        finally
        {
            AdvertiseButton.IsEnabled = true;
            AdvertiseButton.Text = "Start Advertising";
        }
    }

    private async void OnDiscoverClicked(object sender, EventArgs e)
    {
        try
        {
            DiscoverButton.IsEnabled = false;
            DiscoverButton.Text = "Discovering...";

            // TODO: Implement actual discovery logic using the plugin
            // await NearbyConnections.Current.StartDiscoveryAsync();

            await DisplayAlert("Info", "Discovery started. Looking for nearby devices...", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to start discovery: {ex.Message}", "OK");
        }
        finally
        {
            DiscoverButton.IsEnabled = true;
            DiscoverButton.Text = "Start Discovery";
        }
    }

    private async void OnSendMessage(object sender, EventArgs e)
    {
        if (!_isConnected)
        {
            await DisplayAlert("Warning", "Not connected to any peer", "OK");
            return;
        }

        var message = MessageEntry.Text?.Trim();
        if (string.IsNullOrEmpty(message))
            return;

        try
        {
            // Add message to UI
            AddMessage(message, true);

            // TODO: Send message using the plugin
            // await NearbyConnections.Current.SendMessageAsync(message);

            MessageEntry.Text = "";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to send message: {ex.Message}", "OK");
        }
    }

    private void AddMessage(string text, bool isSent)
    {
        var messageFrame = new Border
        {
            BackgroundColor = isSent ? Colors.LightBlue : Colors.LightGray,
            HorizontalOptions = isSent ? LayoutOptions.End : LayoutOptions.Start,
            Margin = new Thickness(isSent ? 50 : 0, 5, isSent ? 0 : 50, 5),
            Padding = new Thickness(15, 10),
        };

        var messageLabel = new Label
        {
            Text = text,
            TextColor = isSent ? Colors.Black : Colors.Black,
            FontSize = 16
        };

        var timeLabel = new Label
        {
            Text = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture),
            TextColor = Colors.Gray,
            FontSize = 12,
            HorizontalOptions = isSent ? LayoutOptions.End : LayoutOptions.Start
        };

        var messageContainer = new StackLayout
        {
            Children = { messageLabel, timeLabel }
        };

        messageFrame.Content = messageContainer;
        MessagesContainer.Children.Add(messageFrame);

        // Scroll to bottom
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(100);
            await chatView.ScrollToAsync(0, chatView.ContentSize.Height, true);
        });
    }

    private void UpdateConnectionStatus(bool isConnected, string peerName)
    {
        _isConnected = isConnected;
        _peerName = peerName;

        ConnectionStatusLabel.Text = isConnected
            ? $"Connected to {peerName}"
            : "Not Connected";

        ConnectionIndicator.TextColor = isConnected ? Colors.Green : Colors.Red;
        SendButton.IsEnabled = isConnected;

        AdvertiseButton.IsEnabled = !isConnected;
        DiscoverButton.IsEnabled = !isConnected;
    }

    // TODO: These methods will be called when the plugin reports connection events
    public void OnPeerConnected(string peerName)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateConnectionStatus(true, peerName);
            AddMessage($"{peerName} joined the chat", false);
        });
    }

    public void OnPeerDisconnected()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateConnectionStatus(false, "");
            AddMessage("Peer disconnected", false);
        });
    }

    public void OnMessageReceived(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            AddMessage(message, false);
        });
    }
}

public class ChatMessage
{
    public string Text { get; set; } = "";
    public bool IsSent { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}