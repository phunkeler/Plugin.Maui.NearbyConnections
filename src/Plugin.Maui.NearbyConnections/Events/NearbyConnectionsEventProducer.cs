using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections.Events;

/// <summary>
/// The default implementation of "INearbyConnectionsEventProducer"/>.
/// </summary>
/// <remarks>
/// TODO: Take this to the plugin options and allow consumers to configure channel behavior.
/// </remarks>
public class NearbyConnectionsEventProducer : INearbyConnectionsEventProducer
{
    readonly Channel<INearbyConnectionsEvent> _channel;

    /// <inheritdoc />
    public ChannelReader<INearbyConnectionsEvent> Events => _channel.Reader;

    /// <summary>
    /// Initializes a new instance of this class.
    /// </summary>
    public NearbyConnectionsEventProducer()
    {
        _channel = Channel.CreateUnbounded<INearbyConnectionsEvent>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = true,
        });
    }

    /// <summary>
    /// Publishes a new event to the channel.
    /// </summary>
    /// <param name="nearbyEvent"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task PublishAsync(INearbyConnectionsEvent nearbyEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nearbyEvent);

        await _channel.Writer.WriteAsync(nearbyEvent, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();

        while (await _channel.Reader.WaitToReadAsync())
        {
            while (_channel.Reader.TryRead(out _)) { }
        }

        GC.SuppressFinalize(this);
    }
}