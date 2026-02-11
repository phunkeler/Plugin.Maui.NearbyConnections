using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NearbyChat.Models;
using Plugin.Maui.BottomSheet.Navigation;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class ChatViewModel : ObservableRecipient, INavigationAware
{
    [ObservableProperty]
    public partial NearbyDevice? Device { get; set; }

    public ObservableCollection<ChatMessage> Messages { get; } =
    [
        new ChatMessage { Text = "Hello!", From = Sender.Peer , Timestamp = DateTimeOffset.Now.AddMinutes(-5)},
        new ChatMessage { Text = "Hi there!", From = Sender.Me, Timestamp = DateTimeOffset.Now.AddMinutes(-4)},
        new ChatMessage { Text = "How are you?", From = Sender.Peer, Timestamp = DateTimeOffset.Now.AddMinutes(-3)},
        new ChatMessage { Text = "I'm good, thanks! How about you?", From = Sender.Me, Timestamp = DateTimeOffset.Now.AddMinutes(-2)},
    ];

    public void OnNavigatedFrom(IBottomSheetNavigationParameters parameters)
        => IsActive = false;

    public void OnNavigatedTo(IBottomSheetNavigationParameters parameters)
    {
        IsActive = true;

        if (parameters.TryGetValue(nameof(NearbyDevice), out var device)
            && device is NearbyDevice nearbyDevice)
        {
            Device = nearbyDevice;
        }
    }
}