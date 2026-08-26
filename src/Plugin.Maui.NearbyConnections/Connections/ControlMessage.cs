namespace Plugin.Maui.NearbyConnections;

enum ControlMessageType : byte
{
    Disconnect = 0x01,

    /// <summary>
    /// Carries the name of a stream payload in-band, ahead of the stream itself — the Android
    /// carrier for story S8, where GMS has no native name field. iOS never sends this: the
    /// MultipeerConnectivity stream API carries the name natively.
    /// </summary>
    StreamName = 0x02,
}

/// <summary>
/// The in-band control frame: <c>[signature(4) | type(1) | body]</c>, little-endian.
/// </summary>
/// <remarks>
/// <b>This layout is a wire contract between peers that may run different plugin versions</b>
/// (settled in <c>docs/ARCHITECTURE.md</c> section 5, stage M6). The header never changes. A
/// frame with an unknown type is logged and ignored, so a newer peer's frames degrade cleanly on
/// this version. The known limitation, accepted there: a peer running a version older than the
/// frame's introduction delivers the frame to the application as an ordinary bytes payload.
/// </remarks>
static class ControlMessage
{
    const uint SIGNATURE = 0x504D4E43; // "PMNC" (Plugin.Maui.NearbyConnections)
    const int HEADER_SIZE = sizeof(uint) + sizeof(byte);

    /// <summary>The most UTF-8 bytes a stream name may occupy on the wire.</summary>
    internal const int MaxStreamNameBytes = 1024;

    internal static byte[] Encode(ControlMessageType type)
    {
        var buffer = new byte[HEADER_SIZE];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, SIGNATURE);
        buffer[sizeof(uint)] = (byte)type;
        return buffer;
    }

    /// <summary>
    /// Encodes a <see cref="ControlMessageType.StreamName"/> frame:
    /// <c>[header | payloadId(8) | nameByteCount(2) | name-utf8]</c>.
    /// </summary>
    /// <param name="payloadId">The platform payload id the stream will arrive under.</param>
    /// <param name="name">The stream's name. At most <see cref="MaxStreamNameBytes"/> UTF-8 bytes.</param>
    internal static byte[] EncodeStreamName(long payloadId, string name)
    {
        var nameBytes = Encoding.UTF8.GetBytes(name);

        if (nameBytes.Length > MaxStreamNameBytes)
        {
            throw new ArgumentException(
                $"The stream name occupies {nameBytes.Length} UTF-8 bytes; the limit is {MaxStreamNameBytes}.",
                nameof(name));
        }

        var buffer = new byte[HEADER_SIZE + sizeof(long) + sizeof(ushort) + nameBytes.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, SIGNATURE);
        buffer[sizeof(uint)] = (byte)ControlMessageType.StreamName;
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(HEADER_SIZE), payloadId);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(HEADER_SIZE + sizeof(long)), (ushort)nameBytes.Length);
        nameBytes.CopyTo(buffer.AsSpan(HEADER_SIZE + sizeof(long) + sizeof(ushort)));

        return buffer;
    }

    internal static bool TryDecode(ReadOnlySpan<byte> data, out ControlMessageType type)
    {
        type = default;

        if (data.Length < HEADER_SIZE
            || BinaryPrimitives.ReadUInt32LittleEndian(data) != SIGNATURE)
        {
            return false;
        }

        type = (ControlMessageType)data[sizeof(uint)];

        // Length validation is per type, and stays strict: an application payload that merely
        // starts with the signature must fall through to payload delivery. An unknown type is
        // accepted only at header length — a longer future frame degrades to an ordinary bytes
        // payload on this version, the same accepted cross-version behavior as the remarks above.
        return type switch
        {
            ControlMessageType.Disconnect => data.Length == HEADER_SIZE,
            ControlMessageType.StreamName => HasValidStreamNameLength(data),
            _ => data.Length == HEADER_SIZE,
        };
    }

    static bool HasValidStreamNameLength(ReadOnlySpan<byte> data)
    {
        if (data.Length < HEADER_SIZE + sizeof(long) + sizeof(ushort))
        {
            return false;
        }

        int nameByteCount = BinaryPrimitives.ReadUInt16LittleEndian(data[(HEADER_SIZE + sizeof(long))..]);

        return nameByteCount <= MaxStreamNameBytes
            && data.Length == HEADER_SIZE + sizeof(long) + sizeof(ushort) + nameByteCount;
    }

    /// <summary>
    /// Decodes the body of a <see cref="ControlMessageType.StreamName"/> frame
    /// <see cref="TryDecode"/> already recognized. A malformed body fails soft — the caller logs
    /// and drops the frame.
    /// </summary>
    /// <param name="data">The whole frame, header included.</param>
    /// <param name="payloadId">The platform payload id the stream will arrive under.</param>
    /// <param name="name">The stream's name.</param>
    internal static bool TryDecodeStreamName(ReadOnlySpan<byte> data, out long payloadId, out string? name)
    {
        payloadId = 0;
        name = null;

        if (data.Length < HEADER_SIZE + sizeof(long) + sizeof(ushort))
        {
            return false;
        }

        payloadId = BinaryPrimitives.ReadInt64LittleEndian(data[HEADER_SIZE..]);
        int nameByteCount = BinaryPrimitives.ReadUInt16LittleEndian(data[(HEADER_SIZE + sizeof(long))..]);

        if (nameByteCount > MaxStreamNameBytes
            || data.Length != HEADER_SIZE + sizeof(long) + sizeof(ushort) + nameByteCount)
        {
            return false;
        }

        // The name is peer-chosen and reaches log sinks and consumer UI — same untrusted class as a
        // display name, so it runs through the same filter. iOS sanitizes at its own callback,
        // which is where its name arrives.
        name = PeerLookup.Sanitize(
            Encoding.UTF8.GetString(data[(HEADER_SIZE + sizeof(long) + sizeof(ushort))..]),
            MaxStreamNameBytes) ?? string.Empty;

        return true;
    }
}
