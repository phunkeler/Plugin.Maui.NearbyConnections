namespace Plugin.Maui.NearbyConnections.UnitTests;

[Trait("Category", "Connections")]
public class ControlMessageTests
{
    /// <summary>
    /// The signature bytes, little-endian, spelling "PMNC" when read as ASCII.
    /// </summary>
    static byte[] SignatureBytes => [0x43, 0x4E, 0x4D, 0x50];

    public sealed class Encode : ControlMessageTests
    {
        [Fact]
        public void Disconnect_ProducesTheExactExpectedBytes()
        {
            // Arrange
            const byte ExpectedDisconnectByte = 0x01;

            // Act
            var encoded = ControlMessage.Encode(ControlMessageType.Disconnect);

            // Assert
            // The frame is a 4-byte signature plus a 1-byte type.
            Assert.Equal(5, encoded.Length);
            // Signature bytes changed — every peer on the previous version will stop recognising control messages.
            Assert.Equal(SignatureBytes, encoded[..4]);
            // Disconnect's wire value changed.
            Assert.Equal(ExpectedDisconnectByte, encoded[4]);
        }

        [Fact]
        public void SignatureOnTheWire_IsPmncReversed()
        {
            // Act
            var encoded = ControlMessage.Encode(ControlMessageType.Disconnect);

            // Assert
            Assert.Equal("CNMP", System.Text.Encoding.ASCII.GetString(encoded[..4]));
        }
    }

    public sealed class TryDecode : ControlMessageTests
    {
        [Fact]
        public void EncodedMessage_RoundTrips()
        {
            // Arrange
            var encoded = ControlMessage.Encode(ControlMessageType.Disconnect);

            // Act
            var decoded = ControlMessage.TryDecode(encoded, out var type);

            // Assert
            Assert.True(decoded);
            Assert.Equal(ControlMessageType.Disconnect, type);
        }

        [Theory]
        [InlineData(0, TestDisplayName = "empty")]
        [InlineData(4, TestDisplayName = "signature only, type byte missing")]
        [InlineData(6, TestDisplayName = "one byte too long")]
        [InlineData(64, TestDisplayName = "an ordinary small payload")]
        public void WrongLength_IsRejected(int length)
        {
            // Arrange
            var buffer = new byte[length];
            SignatureBytes.AsSpan()[..Math.Min(4, length)].CopyTo(buffer);

            // Act
            var decoded = ControlMessage.TryDecode(buffer, out _);

            // Assert
            Assert.False(decoded, $"A {length}-byte buffer was accepted as a control message.");
        }

        [Fact]
        public void WrongSignature_IsRejected()
        {
            // Arrange
            byte[] userPayload = [0x00, 0x01, 0x02, 0x03, 0x01];

            // Act
            var decoded = ControlMessage.TryDecode(userPayload, out _);

            // Assert
            Assert.False(decoded);
        }

        [Fact]
        public void SignatureInWrongByteOrder_IsRejected()
        {
            // Arrange
            byte[] bigEndian = [0x50, 0x4D, 0x4E, 0x43, 0x01];

            // Act
            var decoded = ControlMessage.TryDecode(bigEndian, out _);

            // Assert
            Assert.False(decoded);
        }

        [Fact]
        public void UndefinedType_DecodesSuccessfully_ForForwardCompatibility()
        {
            // Arrange
            byte[] futureType = [.. SignatureBytes, 0xFE];

            // Act
            var decoded = ControlMessage.TryDecode(futureType, out var type);

            // Assert
            Assert.True(decoded);
            Assert.Equal((ControlMessageType)0xFE, type);
            Assert.False(Enum.IsDefined(type), "0xFE should not be a defined type in this build.");
        }

        [Fact]
        public void RejectedMessage_LeavesTypeAtDefault()
        {
            // Arrange
            byte[] notAControlMessage = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF];

            // Act
            ControlMessage.TryDecode(notAControlMessage, out var type);

            // Assert
            Assert.Equal(default, type);
        }
    }
}
