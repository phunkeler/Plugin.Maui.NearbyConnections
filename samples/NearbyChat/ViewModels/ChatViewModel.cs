using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Messages;
using NearbyChat.Models;
using NearbyChat.Services;
using Plugin.Maui.BottomSheet.Navigation;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class ChatViewModel(INearbyConnectionsService nearbyConnectionsService) : ObservableRecipient, INavigationAware,
    IRecipient<DataReceivedMessage>
{
    [ObservableProperty]
    public partial NearbyDevice? Device { get; set; }

    [ObservableProperty]
    public partial string? Message { get; set; }

    [RelayCommand]
    Task Send()
    {
        if (string.IsNullOrWhiteSpace(Message) || Device?.State != NearbyDeviceState.Connected)
        {
            return Task.CompletedTask;
        }

        return nearbyConnectionsService.SendMessage(Device!, Message!);

    }

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

    public void Receive(DataReceivedMessage msg)
    {
        var text = msg?.Payload is BytesPayload bytes
            ? Encoding.Unicode.GetString(bytes.Data)
            : msg?.Payload.ToString() ?? "";

        Messages.Add(new ChatMessage
        {
            Text = text,
            From = Sender.Peer,
            Timestamp = msg?.Timestamp.ToLocalTime() ?? DateTimeOffset.Now
        });
    }
}