using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NearbyChat.Models;

namespace NearbyChat.ViewModels;

public partial class ChatMessageViewModel(ChatMessage model, ILauncher launcher) : ObservableObject
{
    readonly ILauncher _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));

    public ChatMessage Model { get; } = model ?? throw new ArgumentNullException(nameof(model));

    public MediaAttachment? MediaAttachment =>
        Model.Attachments.OfType<MediaAttachment>().FirstOrDefault();

    public ImageSource? Thumbnail => MediaAttachment?.Thumbnail;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsTransferring { get; set; }

    [ObservableProperty]
    public partial double TransferProgress { get; set; }

    public static ChatMessageViewModel Create(ChatMessage model, ILauncher launcher) => model switch
    {
        { Attachments: var attachments } when attachments.Any(a => a.Type == AttachmentType.Photo)
            => new PhotoMessageViewModel(model, launcher),
        { Attachments: var attachments } when attachments.Any(a => a.Type == AttachmentType.Video)
            => new VideoMessageViewModel(model, launcher),
        _ => new ChatMessageViewModel(model, launcher)
    };

    [RelayCommand]
    Task<bool> OpenFile(string filePath)
        => _launcher.OpenAsync(new OpenFileRequest
        {
            Title = Path.GetFileName(filePath),
            File = new ReadOnlyFile(filePath)
        });
}
