using NearbyChat.ViewModels;

namespace NearbyChat.Controls;

public class ChatMessageViewModelDataTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextTemplate { get; set; }
    public DataTemplate? PhotoTemplate { get; set; }
    public DataTemplate? VideoTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        => item switch
        {
            PhotoMessageViewModel => PhotoTemplate ?? throw new InvalidOperationException($"{nameof(PhotoTemplate)} must be set."),
            VideoMessageViewModel => VideoTemplate ?? throw new InvalidOperationException($"{nameof(VideoTemplate)} must be set."),
            ChatMessageViewModel => TextTemplate ?? throw new InvalidOperationException($"{nameof(TextTemplate)} must be set."),
            _ => throw new InvalidOperationException($"No template for type {item.GetType().Name}.")
        };
}