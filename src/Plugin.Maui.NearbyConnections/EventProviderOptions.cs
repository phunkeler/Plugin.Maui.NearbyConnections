using System.Threading.Channels;
using Plugin.Maui.NearbyConnections.Processors;

namespace Plugin.Maui.NearbyConnections;

public class EventProviderOptions
{
    /// <summary>
    /// Gets or sets the maximum capacity of the internal event buffer.
    /// Default is -1, which is unbounded.
    /// </summary>
    public int BufferCapacity { get; set; } = -1;

    /// <summary>
    /// Gets or sets the behavior to use when writing to a bounded
    /// channel that is already full. Default is <see cref="BoundedChannelFullMode.DropOldest"/>.
    /// </summary>
    /// <remarks>
    /// Only applicable when <see cref="BufferCapacity"/> != -1.
    /// </remarks>
    public BoundedChannelFullMode FullMode { get; set; } = BoundedChannelFullMode.DropOldest;

    /// <summary>
    /// An event processor that executes immediately following transformation of the native
    /// event info and before publishing.
    /// </summary>
    public INearbyConnectionsEventProcessor? EventProcessor { get; set; }

    /// <summary>
    /// Whether to complete the channel on first error.
    /// </summary>
    public bool CompleteOnError { get; set; }
}