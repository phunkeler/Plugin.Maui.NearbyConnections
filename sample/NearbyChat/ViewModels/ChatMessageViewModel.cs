using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NearbyChat.Models;

namespace NearbyChat.ViewModels;

public partial class ChatMessageViewModel(ChatMessage model) : ObservableObject
{
    public ChatMessage Model { get; } = model ?? throw new ArgumentNullException(nameof(model));

    // helpers for dealing with attachments
    public IAttachment? FirstAttachment =>
        Model.Attachments.Count > 0 ? Model.Attachments[0] : null;

    public MediaAttachment? MediaAttachment =>
        Model.Attachments.OfType<MediaAttachment>().FirstOrDefault();

    public ImageSource? Thumbnail => MediaAttachment?.Thumbnail;

    public bool HasMedia => MediaAttachment is not null;

    public bool IsPhoto => MediaAttachment is PhotoAttachment;

    public bool IsVideo => MediaAttachment is VideoAttachment;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [RelayCommand]
    Task<bool> OpenFile(string filePath)
        => Launcher.Default.OpenAsync(new OpenFileRequest
        {
            Title = Path.GetFileName(filePath),
            File = new ReadOnlyFile(filePath)
        });
}