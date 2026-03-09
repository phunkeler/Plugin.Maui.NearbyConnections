using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
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
    IDispatcher dispatcher,
    IMediaPicker mediaPicker,
    IThumbnailService thumbnailService,
    IChatMessageRepository chatMessageRepository,
    IChatMessageViewModelFactory chatMessageViewModelFactory,
    IChatMessageService chatMessageService) : ObservableRecipient,
    INavigationAware,
    IRecipient<ChatMessageReceived>,
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
    [NotifyPropertyChangedFor(nameof(CanSend))]
    public partial string? Message { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial NearbyTransferStatus? TransferStatus { get; set; }

    [ObservableProperty]
    public partial FileResult? SelectedFile { get; set; }

    [ObservableProperty]
    public partial MediaAttachment? MediaAttachment { get; set; }

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

        await chatMessageService.SendChatMessage(Device, chatMessage);
        var vm = chatMessageViewModelFactory.Create(chatMessage);
        Messages.Add(vm);
    }

    [RelayCommand]
    async Task Attach()
    {
        const string photoOption = "Photo";
        const string videoOption = "Video";

        var choice = await Shell.Current.DisplayActionSheetAsync(
            title: "Attach",
            cancel: "Cancel",
            destruction: null,
            photoOption,
            videoOption);

        if (choice is photoOption)
        {
            var photo = await mediaPicker.PickPhotosAsync();

            if (photo?.FirstOrDefault() is FileResult fileResult)
            {
                var fullPath = fileResult.FullPath;

                if (OperatingSystem.IsIOS())
                {
                    fullPath = await CreateTempFile(fileResult);
                }

                var photoAttachment = new PhotoAttachment
                {
                    FilePath = fullPath,
                    Thumbnail = ImageSource.FromFile(fullPath)
                };

                MediaAttachment = photoAttachment;
                Message = fileResult.FileName;
            }
        }
        else
        {
            var video = await mediaPicker.PickVideosAsync();

            if (video?.FirstOrDefault() is FileResult fileResult)
            {
                var fullPath = fileResult.FullPath;

                if (OperatingSystem.IsIOS())
                {
                    fullPath = await CreateTempFile(fileResult);
                }

                var thumbnail = await thumbnailService.GetVideoThumbnailAsync(fullPath);

                var videoAttachment = new VideoAttachment
                {
                    FilePath = fullPath,
                    Thumbnail = thumbnail
                };

                MediaAttachment = videoAttachment;
                Message = fileResult.FileName;
            }
        }
    }

    public void OnNavigatedFrom(IBottomSheetNavigationParameters parameters)
        => IsActive = false;

    public void OnNavigatedTo(IBottomSheetNavigationParameters parameters)
    {
        IsActive = true;

        if (parameters.TryGetValue(nameof(NearbyDevice), out var device)
            && device is NearbyDevice nearbyDevice)
        {
            Device = nearbyDevice;
            _ = Task.Run(() => LoadHistoryAsync(nearbyDevice));
        }
    }

    static async Task<string> CreateTempFile(FileResult fileResult)
    {
        // On iOS, FullPath may be just a filename.
        // Copy via stream to a known local path before sending.
        var localPath = Path.Combine(FileSystem.CacheDirectory, fileResult.FileName);
        using (var source = await fileResult.OpenReadAsync())
        using (var dest = File.Create(localPath))
        {
            await source.CopyToAsync(dest);
        }

        return localPath;
    }

    Task LoadHistoryAsync(NearbyDevice device)
    {
        Messages.Clear();

        foreach (var message in chatMessageRepository.GetAll(device))
        {
            var vm = chatMessageViewModelFactory.Create(message);
            Messages.Add(vm);
        }

        return Task.CompletedTask;
    }

    public void Receive(ChatMessageReceived receivedMsg)
    {
        if (receivedMsg.Value.Id != Device?.Id)
        {
            return;
        }

        var stored = chatMessageRepository.GetAll(receivedMsg.Value);

        if (stored.Count <= 0)
        {
            return;
        }

        var message = stored[^1];
        var vm = chatMessageViewModelFactory.Create(message);
        Messages.Add(vm);
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