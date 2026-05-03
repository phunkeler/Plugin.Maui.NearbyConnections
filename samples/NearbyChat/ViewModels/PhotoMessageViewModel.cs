using NearbyChat.Models;

namespace NearbyChat.ViewModels;

public sealed class PhotoMessageViewModel(ChatMessage model)
    : ChatMessageViewModel(model)
{
    public PhotoAttachment? Attachment =>
        Model.Attachments.OfType<PhotoAttachment>().FirstOrDefault();

    public new ImageSource? Thumbnail => Attachment?.Thumbnail;
}