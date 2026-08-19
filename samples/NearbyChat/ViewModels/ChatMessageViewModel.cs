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

    /// <summary>
    /// Why this message did not reach the other device, or <see langword="null"/> while it is in
    /// flight or delivered.
    /// </summary>
    /// <remarks>
    /// Carried on the bubble rather than raised as an alert, so the failure stays attached to the
    /// message it belongs to. A user who sends three messages during a dropout can see which ones
    /// landed.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailed))]
    public partial string? FailureReason { get; set; }

    /// <summary>
    /// What the user can do about <see cref="FailureReason"/>, phrased as an instruction.
    /// </summary>
    [ObservableProperty]
    public partial string? FailureRemedy { get; set; }

    /// <summary>
    /// Whether this message failed to send and is showing its reason.
    /// </summary>
    public bool HasFailed => FailureReason is not null;

    /// <summary>
    /// Why the last attempt to open this message's attachment failed, or <see langword="null"/> if
    /// none has.
    /// </summary>
    /// <remarks>
    /// Deliberately separate from <see cref="FailureReason"/>. That pair means "this message did
    /// not reach the other device" and renders a Retry button that sends it again. A file that no
    /// installed app can open is a local problem, and re-sending is the wrong remedy for it.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOpenFailed))]
    public partial string? OpenFailureReason { get; set; }

    /// <summary>
    /// Whether the last attempt to open this message's attachment failed.
    /// </summary>
    public bool HasOpenFailed => OpenFailureReason is not null;

    /// <summary>
    /// Clears the failure state so the bubble can be shown as in flight again.
    /// </summary>
    public void ClearFailure()
    {
        FailureReason = null;
        FailureRemedy = null;
    }

    /// <summary>
    /// Records why this message failed and what the user should do about it.
    /// </summary>
    public void Fail(string reason, string remedy)
    {
        FailureReason = reason;
        FailureRemedy = remedy;
    }

    public static ChatMessageViewModel Create(ChatMessage model, ILauncher launcher) => model switch
    {
        { Attachments: var attachments } when attachments.Any(a => a.Type == AttachmentType.Photo)
            => new PhotoMessageViewModel(model, launcher),
        { Attachments: var attachments } when attachments.Any(a => a.Type == AttachmentType.Video)
            => new VideoMessageViewModel(model, launcher),
        _ => new ChatMessageViewModel(model, launcher)
    };

    [RelayCommand]
    async Task OpenFile(string filePath)
    {
        OpenFailureReason = null;

        // OpenAsync reports "no app handled this file" by returning false rather than throwing.
        // Discarding the result means a tap that opens nothing and explains nothing.
        var opened = await _launcher.OpenAsync(new OpenFileRequest
        {
            Title = Path.GetFileName(filePath),
            File = new ReadOnlyFile(filePath)
        });

        if (!opened)
        {
            OpenFailureReason = "No app on this device can open this file.";
        }
    }
}
