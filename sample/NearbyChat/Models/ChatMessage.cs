namespace NearbyChat.Models;

public enum NearbyDirection
{
    Incoming,
    Outgoing
}

public record ChatMessage(string? Text, NearbyDirection Direction, DateTimeOffset Timestamp)
{
    public IList<IAttachment> Attachments { get; } = [];
}

public interface IAttachment
{
    AttachmentType Type { get; }
}

public enum AttachmentType
{
    Photo,
    Video,
    Other
}

public abstract class MediaAttachment : IAttachment
{
    public abstract AttachmentType Type { get; }
    public string? FilePath { get; set; }
    public ImageSource? Thumbnail { get; set; }
}

public sealed class PhotoAttachment : MediaAttachment
{
    public override AttachmentType Type => AttachmentType.Photo;
}

public sealed class VideoAttachment : MediaAttachment
{
    public override AttachmentType Type => AttachmentType.Video;
    public TimeSpan? Duration { get; set; }
}