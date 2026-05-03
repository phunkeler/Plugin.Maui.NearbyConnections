using NearbyChat.Models;

namespace NearbyChat.ViewModels;

public sealed class VideoMessageViewModel(ChatMessage model)
    : ChatMessageViewModel(model)
{
    public VideoAttachment? Attachment =>
        Model.Attachments.OfType<VideoAttachment>().FirstOrDefault();

    public new ImageSource? Thumbnail => Attachment?.Thumbnail;

    public TimeSpan? Duration => Attachment?.Duration;
}