namespace Plugin.Maui.NearbyConnections;

enum ControlMessageType : byte
{
    Disconnect = 0x01,
    StreamName = 0x02,
    ConnectRequest = 0x03,
}

// The in-band control frame: [ signature(4) | type(1) | body ], little-endian.
static class ControlMessage
{
    const uint SIGNATURE = 0x504D4E43; // "PMNC" (Plugin.Maui.NearbyConnections)

    static class Offset
    {
        internal const int Type = sizeof(uint);
        internal const int Body = Type + sizeof(byte);

        // StreamName body: [ payloadId(8) | nameByteCount(2) | name-utf8 ]
        internal const int StreamPayloadId = Body;
        internal const int StreamNameCount = StreamPayloadId + sizeof(long);
        internal const int StreamNameText = StreamNameCount + sizeof(ushort);

        // ConnectRequest body: [offerWindowMs(4) | name-utf8 (rest)]
        internal const int OfferWindow = Body;
        internal const int ConnectNameText = OfferWindow + sizeof(uint);
    }

    internal const int MaxStreamNameBytes = 1024;
    internal const uint UnboundedOfferWindow = 0xFFFFFFFF;

    static byte[] NewFrame(ControlMessageType type, int bodySize)
    {
        var frame = new byte[Offset.Body + bodySize];

        BinaryPrimitives.WriteUInt32LittleEndian(frame, SIGNATURE);
        frame[Offset.Type] = (byte)type;

        return frame;
    }

    static bool TryReadHeader(ReadOnlySpan<byte> data, out ControlMessageType type)
    {
        if (data.Length < Offset.Body
            || BinaryPrimitives.ReadUInt32LittleEndian(data) != SIGNATURE)
        {
            type = default;
            return false;
        }

        type = (ControlMessageType)data[Offset.Type];

        return true;
    }

    static void ThrowIfTooLong(
        ReadOnlySpan<byte> utf8,
        int maxBytes,
        string what,
        string parameterName)
    {
        if (utf8.Length > maxBytes)
        {
            throw new ArgumentException(
                $"The {what} occupies {utf8.Length} UTF-8 bytes; the limit is {maxBytes}.",
                parameterName);
        }
    }

    internal static byte[] Encode(ControlMessageType type) => NewFrame(type, bodySize: 0);

    internal static byte[] EncodeStreamName(long payloadId, string name)
    {
        var nameBytes = Encoding.UTF8.GetBytes(name);

        ThrowIfTooLong(
            nameBytes,
            MaxStreamNameBytes,
            nameof(ControlMessageType.StreamName),
            nameof(name));

        var frame = NewFrame(
            ControlMessageType.StreamName,
            bodySize: sizeof(long) + sizeof(ushort) + nameBytes.Length);

        BinaryPrimitives.WriteInt64LittleEndian(frame.AsSpan(Offset.StreamPayloadId), payloadId);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(Offset.StreamNameCount), (ushort)nameBytes.Length);
        nameBytes.CopyTo(frame.AsSpan(Offset.StreamNameText));

        return frame;
    }

    internal static byte[] EncodeConnectRequest(TimeSpan offerWindow, string displayName)
    {
        var nameBytes = Encoding.UTF8.GetBytes(displayName);

        ThrowIfTooLong(
            nameBytes,
            DisplayNameRules.MaxBytes,
            $"{nameof(NearbyOptions.DisplayName)}",
            nameof(displayName));

        var frame = NewFrame(
            ControlMessageType.ConnectRequest,
            bodySize: sizeof(uint) + nameBytes.Length);

        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(Offset.OfferWindow), ToWireWindow(offerWindow));
        nameBytes.CopyTo(frame.AsSpan(Offset.ConnectNameText));

        return frame;
    }

    static uint ToWireWindow(TimeSpan offerWindow)
    {
        if (offerWindow == Timeout.InfiniteTimeSpan)
        {
            return UnboundedOfferWindow;
        }

        if (offerWindow <= TimeSpan.Zero)
        {
            return 0u;
        }

        return offerWindow.TotalMilliseconds >= UnboundedOfferWindow
            ? UnboundedOfferWindow - 1
            : (uint)offerWindow.TotalMilliseconds;
    }

    internal static bool TryDecode(ReadOnlySpan<byte> data, out ControlMessageType type)
    {
        if (!TryReadHeader(data, out type))
        {
            return false;
        }

        return type switch
        {
            ControlMessageType.Disconnect => data.Length == Offset.Body,
            ControlMessageType.StreamName => HasValidStreamNameLength(data),
            ControlMessageType.ConnectRequest => data.Length >= Offset.ConnectNameText,
            _ => data.Length == Offset.Body,
        };
    }

    static bool HasValidStreamNameLength(ReadOnlySpan<byte> data)
        => TryReadStreamNameCount(data, out var nameByteCount)
            && data.Length == Offset.StreamNameText + nameByteCount;

    static bool TryReadStreamNameCount(ReadOnlySpan<byte> data, out int nameByteCount)
    {
        nameByteCount = 0;

        if (data.Length < Offset.StreamNameText)
        {
            return false;
        }

        nameByteCount = BinaryPrimitives.ReadUInt16LittleEndian(data[Offset.StreamNameCount..]);

        return nameByteCount <= MaxStreamNameBytes;
    }

    internal static bool TryDecodeConnectRequest(
        ReadOnlySpan<byte> data,
        out TimeSpan offerWindow,
        [NotNullWhen(true)] out string? displayName)
    {
        offerWindow = default;
        displayName = null;

        if (data.Length < Offset.ConnectNameText
            || !TryReadHeader(data, out var type)
            || type != ControlMessageType.ConnectRequest)
        {
            return false;
        }

        var windowMs = BinaryPrimitives.ReadUInt32LittleEndian(data[Offset.OfferWindow..]);

        offerWindow = windowMs == UnboundedOfferWindow
            ? Timeout.InfiniteTimeSpan
            : TimeSpan.FromMilliseconds(windowMs);
        displayName = Encoding.UTF8.GetString(data[Offset.ConnectNameText..]);

        return true;
    }

    internal static bool TryDecodeStreamName(ReadOnlySpan<byte> data, out long payloadId, out string? name)
    {
        payloadId = 0;
        name = null;

        if (!HasValidStreamNameLength(data))
        {
            return false;
        }

        payloadId = BinaryPrimitives.ReadInt64LittleEndian(data[Offset.StreamPayloadId..]);

        name = PeerLookup.Sanitize(
            Encoding.UTF8.GetString(data[Offset.StreamNameText..]),
            MaxStreamNameBytes) ?? string.Empty;

        return true;
    }
}
