using NearbyChat.Models;

namespace NearbyChat.Controls;

public class ChatMessageDataTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextTemplate { get; set; }
    public DataTemplate? FileTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        => item is ChatMessage { IsFileMessage: true }
            ? FileTemplate ?? throw new InvalidOperationException("FileTemplate must be set.")
            : TextTemplate ?? throw new InvalidOperationException("TextTemplate must be set.");

}