using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NearbyChat.Data;
using NearbyChat.Messages;
using NearbyChat.Models;
using NearbyChat.Services;
using Plugin.Maui.BottomSheet.Navigation;
using Plugin.Maui.NearbyConnections;

namespace NearbyChat.ViewModels;

public partial class ChatViewModel(
    IMessenger messenger,
    IDispatcher dispatcher,
    ILauncher launcher,
    IMediaPicker mediaPicker,
    INavigationService navigationService,
    ChatMessageStore store,
    INearby session) : ObservableRecipient(messenger),
    INavigationAware,
    IRecipient<ChatMessageReceived>,
    IRecipient<InboundTransferProgress>
{
    [MemberNotNullWhen(true, nameof(Device), nameof(Message))]
    public bool CanSend
        => Device is not null
            && !string.IsNullOrWhiteSpace(Message);

    [ObservableProperty]
    public partial NearbyDevice? Device { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyPropertyChangedFor(nameof(CanSend))]
    public partial string? Message { get; set; }

    [ObservableProperty]
    public partial MediaAttachment? MediaAttachment { get; set; }

    [ObservableProperty]
    public partial bool IsReceiving { get; set; }

    [ObservableProperty]
    public partial double ReceiveProgress { get; set; }

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    [RelayCommand(
        CanExecute = nameof(CanSend),
        IncludeCancelCommand = true)]
    async Task Send(CancellationToken cancellationToken)
    {
        if (!CanSend)
        {
            return;
        }

        var chatMessage = new ChatMessage(Message, NearbyDirection.Outgoing, DateTimeOffset.UtcNow);

        if (MediaAttachment is not null)
        {
            chatMessage.Attachments.Add(MediaAttachment);
        }

        Message = null;

        // Add the bubble before awaiting the send so outbound transfer progress
        // has a message to render against while the file is in flight.
        var vm = ChatMessageViewModel.Create(chatMessage, launcher);
        Messages.Add(vm);

        IProgress<NearbyTransferProgress>? progress = null;

        if (vm.MediaAttachment is not null)
        {
            vm.IsTransferring = true;
            progress = new OutboundProgressRelay(dispatcher, vm);
        }

        try
        {
            store.Add(Device.Id, chatMessage);

            if (session.TryGetConnection(Device.Id, out var connection))
            {
                if (chatMessage.Attachments.FirstOrDefault() is MediaAttachment { FilePath: { Length: > 0 } filePath })
                {
                    await connection.SendAsync(filePath, progress, cancellationToken);
                }
                else if (!string.IsNullOrWhiteSpace(chatMessage.Text))
                {
                    await connection.SendAsync(Encoding.UTF8.GetBytes(chatMessage.Text), cancellationToken);
                }
            }
        }
        finally
        {
            vm.IsTransferring = false;
        }
    }

    [RelayCommand]
    async Task Attach()
    {
        const string photoOption = "Photo";
        const string videoOption = "Video";

        var choice = await navigationService.DisplayActionSheetAsync(
            title: "Attach",
            cancel: "Cancel",
            destruction: null,
            photoOption,
            videoOption);

        if (choice is not (photoOption or videoOption))
        {
            return;
        }

        var attachment = await PickAsync(video: choice is videoOption);

        if (attachment is not null)
        {
            MediaAttachment = attachment;
        }

        async Task<MediaAttachment?> PickAsync(bool video)
        {
            var files = video
                ? await mediaPicker.PickVideosAsync()
                : await mediaPicker.PickPhotosAsync();

            if (files?.FirstOrDefault() is not FileResult fileResult)
            {
                return null;
            }

            var fullPath = fileResult.FullPath;

            if (OperatingSystem.IsIOS())
            {
                fullPath = await CreateTempFile(fileResult);
            }

            MediaAttachment picked = video
                ? new VideoAttachment
                {
                    FilePath = fullPath,
                    Thumbnail = await ThumbnailService.GetVideoThumbnailAsync(fullPath)
                }
                : new PhotoAttachment
                {
                    FilePath = fullPath,
                    Thumbnail = ImageSource.FromFile(fullPath)
                };

            Message = fileResult.FileName;
            return picked;
        }
    }

    public void OnNavigatedFrom(IBottomSheetNavigationParameters parameters)
        => IsActive = false;

    public void OnNavigatedTo(IBottomSheetNavigationParameters parameters)
    {
        IsActive = true;
        IsReceiving = false;
        ReceiveProgress = 0;

        if (parameters.TryGetValue(nameof(NearbyDevice), out var device)
            && device is NearbyDevice nearbyDevice)
        {
            Device = nearbyDevice;

            Messages.Clear();

            foreach (var message in store.GetAll(nearbyDevice.Id))
            {
                Messages.Add(ChatMessageViewModel.Create(message, launcher));
            }
        }
    }

    static async Task<string> CreateTempFile(FileResult fileResult)
    {
        // On iOS, FullPath may be just a filename.
        // Copy via stream to a known local path before sending.
        var localPath = Path.Combine(FileSystem.CacheDirectory, fileResult.FileName);
        await using var source = await fileResult.OpenReadAsync();
        await using var dest = File.Create(localPath);
        await source.CopyToAsync(dest);

        return localPath;
    }

    public void Receive(ChatMessageReceived receivedMsg)
    {
        if (receivedMsg.Device.Id != Device?.Id)
        {
            return;
        }

        // Published from the ingestion loop's thread — marshal before touching the bound
        // collection.
        dispatcher.Dispatch(() =>
        {
            IsReceiving = false;
            ReceiveProgress = 0;
            Messages.Add(ChatMessageViewModel.Create(receivedMsg.Message, launcher));
        });
    }

    public void Receive(InboundTransferProgress progressMsg)
    {
        if (progressMsg.Device.Id != Device?.Id)
        {
            return;
        }

        dispatcher.Dispatch(() =>
        {
            // Inbound transfers only ever report InProgress; the transfer is complete
            // when the materialized message arrives via ChatMessageReceived, which
            // clears this indicator.
            IsReceiving = true;
            ReceiveProgress = progressMsg.Progress.Fraction ?? 0;
        });
    }

    sealed class OutboundProgressRelay(IDispatcher dispatcher, ChatMessageViewModel message) : IProgress<NearbyTransferProgress>
    {
        public void Report(NearbyTransferProgress value)
            => dispatcher.Dispatch(() =>
            {
                if (value.Fraction is { } fraction)
                {
                    message.TransferProgress = fraction;
                }

                if (value.Status is not NearbyTransferStatus.InProgress)
                {
                    message.IsTransferring = false;
                }
            });
    }
}
