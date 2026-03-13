namespace Plugin.Maui.NearbyConnections;

/// <summary>
/// Identifies the type of an internal control message.
/// </summary>
enum ControlMessageType : byte
{
    /// <summary>
    /// Instructs the receiving peer to disconnect itself from the session.
    /// </summary>
    Disconnect = 0x01,
}

/// <summary>
/// Encodes and decodes internal PMNC control messages.
/// Wire format: [uint32-LE signature] [byte type]
/// </summary>
internal static class ControlMessage
{
    const uint SIGNATURE = 0x504D4E43; // = "PMNC" (Plugin.Maui.NearbyConnections)
    const int SIZE = sizeof(uint) + sizeof(byte);

    internal static byte[] Encode(ControlMessageType type)
    {
        var buffer = new byte[SIZE];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, SIGNATURE);
        buffer[sizeof(uint)] = (byte)type;
        return buffer;
    }

    internal static bool TryDecode(ReadOnlySpan<byte> data, out ControlMessageType type)
    {
        type = default;

        if (data.Length != SIZE
            || BinaryPrimitives.ReadUInt32LittleEndian(data) != SIGNATURE)
        {
            return false;
        }

        type = (ControlMessageType)data[sizeof(uint)];
        return true;
    }
}
