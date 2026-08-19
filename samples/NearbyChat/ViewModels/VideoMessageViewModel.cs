using NearbyChat.Models;

namespace NearbyChat.ViewModels;

public sealed class VideoMessageViewModel(ChatMessage model, ILauncher launcher)
    : ChatMessageViewModel(model, launcher)
{
    public VideoAttachment? Attachment =>
        Model.Attachments.OfType<VideoAttachment>().FirstOrDefault();

    public TimeSpan? Duration => Attachment?.Duration;
}