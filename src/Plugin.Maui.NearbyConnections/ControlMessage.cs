namespace Plugin.Maui.NearbyConnections;

enum ControlMessageType : byte
{
    Disconnect = 0x01,
}

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
