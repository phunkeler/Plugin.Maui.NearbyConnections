using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Lock-free fan-out broadcaster for <see cref="System.Threading.Channels"/>.
/// All methods must be called while the caller holds its own state lock.
/// </summary>
internal sealed class ChannelBroadcaster<T>
{
    readonly List<Channel<T>> _channels = [];

    /// <summary>Creates a new subscriber channel and registers it for fan-out.</summary>
    internal Channel<T> Subscribe()
    {
        var ch = Channel.CreateUnbounded<T>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
        _channels.Add(ch);
        return ch;
    }

    /// <summary>Removes a subscriber channel from fan-out and completes it.</summary>
    internal void Unsubscribe(Channel<T> ch)
    {
        _channels.Remove(ch);
        ch.Writer.TryComplete();
    }

    /// <summary>Writes <paramref name="item"/> to every subscriber channel.</summary>
    internal void Publish(T item)
    {
        foreach (var ch in _channels)
        {
            ch.Writer.TryWrite(item);
        }
    }

    /// <summary>Completes all subscriber channels, optionally with a fault.</summary>
    internal void Complete(Exception? fault = null)
    {
        foreach (var ch in _channels)
        {
            ch.Writer.TryComplete(fault);
        }
        _channels.Clear();
    }
}
