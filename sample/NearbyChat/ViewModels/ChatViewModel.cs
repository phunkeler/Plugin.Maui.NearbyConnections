using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

public partial class ChatViewModel(
    IDispatcher dispatcher,
    IMediaPicker mediaPicker,
    INearbyConnectionsService nearbyConnectionsService) : ObservableRecipient,
    INavigationAware,
    IRecipient<DataReceivedMessage>,
    IRecipient<DeviceStateChangedMessage>
{
    [MemberNotNullWhen(true, nameof(Message))]
    public bool CanSend
        => Device?.State == NearbyDeviceState.Connected
            && !string.IsNullOrWhiteSpace(Message)
            && TransferStatus is not NearbyTransferStatus.InProgress;

    [ObservableProperty]
    public partial NearbyDevice Device { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string? Message { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial NearbyTransferStatus? TransferStatus { get; set; }

    [ObservableProperty]
    public partial FileResult? SelectedFile { get; set; }

    public ObservableCollection<ChatMessage> Messages { get; } =
    [
        new ChatMessage { Text = "Hello!", From = Sender.Peer , Timestamp = DateTimeOffset.Now.AddMinutes(-5)},
        new ChatMessage { Text = "Hi there!", From = Sender.Me, Timestamp = DateTimeOffset.Now.AddMinutes(-4)},
        new ChatMessage { Text = "How are you?", From = Sender.Peer, Timestamp = DateTimeOffset.Now.AddMinutes(-3)},
        new ChatMessage { Text = "I'm good, thanks! How about you?", From = Sender.Me, Timestamp = DateTimeOffset.Now.AddMinutes(-2)},
    ];

    [RelayCommand(
        CanExecute = nameof(CanSend),
        IncludeCancelCommand = true)]
    async Task Send(CancellationToken cancellationToken)
    {
        if (!CanSend)
        {
            return;
        }

        if (SelectedFile is not null)
        {
            var file = SelectedFile;
            Message = null;
            SelectedFile = null;

            var progress = new Progress<NearbyTransferProgress>(OnNearbyTransferProgress);

            await nearbyConnectionsService.SendAsync(
                device: Device,
                file.FullPath,
                progress: progress,
                cancellationToken: cancellationToken);

            Messages.Add(new ChatMessage
            {
                Text = file.FileName,
                FilePath = file.FullPath,
                From = Sender.Me,
                Timestamp = DateTimeOffset.Now
            });

            Trace.TraceInformation("The end");
        }
        else
        {
            await nearbyConnectionsService.SendMessage(Device, Message);
        }
    }

    [RelayCommand]
    async Task Attach()
    {
        var fileResult = await mediaPicker.PickPhotosAsync();

        if (fileResult?.FirstOrDefault() is not FileResult file)
        {
            return;
        }

        SelectedFile = file;
        Message = SelectedFile.FileName;
    }

    [RelayCommand]
    Task<bool> OpenFile(string filePath)
        => Launcher.Default.OpenAsync(new OpenFileRequest
        {
            Title = Path.GetFileName(filePath),
            File = new ReadOnlyFile(filePath)
        });

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
        if (msg.Payload is BytesPayload bytes)
        {
            var text = Encoding.UTF8.GetString(bytes.Data);
            Messages.Add(new ChatMessage
            {
                Text = text,
                From = Sender.Peer,
                Timestamp = msg.Timestamp.ToLocalTime()
            });
        }
        else if (msg.Payload is FilePayload file)
        {
            Messages.Add(new ChatMessage
            {
                Text = file.FileResult.FileName,
                FilePath = file.FileResult.FullPath,
                From = Sender.Peer,
                Timestamp = msg.Timestamp.ToLocalTime()
            });
        }
        else if (msg.Payload is StreamPayload stream)
        {
            Messages.Add(new ChatMessage
            {
                Text = stream.Name,
                From = Sender.Peer,
                Timestamp = msg.Timestamp.ToLocalTime()
            });
        }
    }

    public async void Receive(DeviceStateChangedMessage message)
        => await dispatcher.DispatchAsync(() =>
            {
                if (message.Value.Id == Device?.Id)
                {
                    SendCommand.NotifyCanExecuteChanged();
                }
            });

    void OnNearbyTransferProgress(NearbyTransferProgress progress)
        => TransferStatus = progress.Status;
}