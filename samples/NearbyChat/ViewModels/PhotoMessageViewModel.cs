using NearbyChat.Models;

namespace NearbyChat.ViewModels;

public sealed class PhotoMessageViewModel(ChatMessage model, ILauncher launcher)
    : ChatMessageViewModel(model, launcher)
{
    public PhotoAttachment? Attachment =>
        Model.Attachments.OfType<PhotoAttachment>().FirstOrDefault();
}