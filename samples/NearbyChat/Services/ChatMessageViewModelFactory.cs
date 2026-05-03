using System;
using NearbyChat.Models;
using NearbyChat.ViewModels;

namespace NearbyChat.Services;

public interface IChatMessageViewModelFactory
{
    ChatMessageViewModel Create(ChatMessage model);
}

public class ChatMessageViewModelFactory : IChatMessageViewModelFactory
{
    public ChatMessageViewModel Create(ChatMessage model)
    {
        var hasAttachments = model.Attachments.Count > 0;
        ChatMessageViewModel? vm;

        if (hasAttachments
            && model.Attachments.Any(a => a.Type == AttachmentType.Photo))
        {
            vm = new PhotoMessageViewModel(model);
        }
        else if (hasAttachments
            && model.Attachments.Any(a => a.Type == AttachmentType.Video))
        {
            vm = new VideoMessageViewModel(model);
        }
        else
        {
            vm = new ChatMessageViewModel(model);
        }

        return vm;
    }
}
