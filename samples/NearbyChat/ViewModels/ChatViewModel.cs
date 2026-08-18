using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
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
    INearby session,
    ILogger<ChatViewModel> logger) : ObservableRecipient(messenger),
    INavigationAware,
    IRecipient<ChatMessageReceived>,
    IRecipient<InboundTransferProgress>
{
    readonly ILogger<ChatViewModel> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
        MediaAttachment = null;

        // Add the bubble before awaiting the send so outbound transfer progress
        // has a message to render against while the file is in flight.
        var vm = ChatMessageViewModel.Create(chatMessage, launcher);
        Messages.Add(vm);

        store.Add(Device.Id, chatMessage);

        await DeliverAsync(vm, cancellationToken);
    }

    /// <summary>
    /// Sends the message a bubble already shows, and records on that bubble why it did not arrive.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Send"/> so <see cref="Retry"/> can re-run delivery for a bubble that
    /// is already in the list and already in the store.
    /// </remarks>
    async Task DeliverAsync(ChatMessageViewModel vm, CancellationToken cancellationToken)
    {
        if (Device is null)
        {
            return;
        }

        vm.ClearFailure();

        IProgress<NearbyTransferProgress>? progress = null;

        if (vm.MediaAttachment is not null)
        {
            vm.IsTransferring = true;
            progress = new OutboundProgressRelay(dispatcher, vm);
        }

        try
        {
            // Checked here rather than before the bubble is added: a message the user typed is
            // never silently dropped, it is shown as failed with a reason they can act on.
            if (!session.TryGetConnection(Device.Id, out var connection))
            {
                vm.Fail(
                    "Not delivered. The connection to this device has ended.",
                    "Reconnect from the Connections screen, then tap Retry.");
                return;
            }

            if (vm.Model.Attachments.FirstOrDefault() is MediaAttachment { FilePath: { Length: > 0 } filePath })
            {
                await connection.SendAsync(filePath, progress, cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(vm.Model.Text))
            {
                await connection.SendAsync(Encoding.UTF8.GetBytes(vm.Model.Text), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            vm.Fail(
                "Not delivered. You cancelled this send.",
                "Tap Retry to send it again.");
        }
        catch (NearbyTransferTimeoutException ex)
        {
            LogSendFailed(Device.Id, ex);

            vm.Fail(
                "Not delivered. The transfer stalled and no data moved for too long.",
                "Move the devices closer together, then tap Retry.");
        }
        catch (NearbyTransferException ex)
        {
            LogSendFailed(Device.Id, ex);

            vm.Fail(
                "Not delivered. The file could not be sent.",
                "Check the file still exists, then tap Retry.");
        }
        catch (NearbyException ex)
        {
            // The base type covers the disconnect-mid-send case, which every SendAsync overload
            // documents but no more specific exception represents.
            LogSendFailed(Device.Id, ex);

            vm.Fail(
                "Not delivered. The connection dropped while sending.",
                "Reconnect from the Connections screen, then tap Retry.");
        }
        finally
        {
            vm.IsTransferring = false;
        }
    }

    /// <summary>
    /// Sends a failed message again.
    /// </summary>
    [RelayCommand]
    Task Retry(ChatMessageViewModel vm)
        => DeliverAsync(vm, CancellationToken.None);

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

    [LoggerMessage(
        EventId = 1,
        EventName = nameof(LogSendFailed),
        Level = LogLevel.Error,
        Message = "Sending a message to {DeviceId} failed.")]
    partial void LogSendFailed(string deviceId, Exception exception);

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
