namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestCategory("Connections")]
public class ControlMessageTests
{
    /// <summary>
    /// The signature bytes, little-endian, spelling "PMNC" when read as ASCII.
    /// </summary>
    static byte[] SignatureBytes => [0x43, 0x4E, 0x4D, 0x50];

    [TestClass]
    public sealed class Encode : ControlMessageTests
    {
        [TestMethod]
        public void Disconnect_ProducesTheExactExpectedBytes()
        {
            // Arrange
            const byte ExpectedDisconnectByte = 0x01;

            // Act
            var encoded = ControlMessage.Encode(ControlMessageType.Disconnect);

            // Assert
            Assert.HasCount(5, encoded, "The frame is a 4-byte signature plus a 1-byte type.");
            Assert.AreSequenceEqual(
                SignatureBytes, encoded[..4], "Signature bytes changed — every peer on the previous version will stop recognising control messages.");
            Assert.AreEqual(ExpectedDisconnectByte, encoded[4], "Disconnect's wire value changed.");
        }

        [TestMethod]
        public void SignatureOnTheWire_IsPmncReversed()
        {
            // Act
            var encoded = ControlMessage.Encode(ControlMessageType.Disconnect);

            // Assert
            Assert.AreEqual("CNMP", System.Text.Encoding.ASCII.GetString(encoded[..4]));
        }
    }

    [TestClass]
    public sealed class TryDecode : ControlMessageTests
    {
        [TestMethod]
        public void EncodedMessage_RoundTrips()
        {
            // Arrange
            var encoded = ControlMessage.Encode(ControlMessageType.Disconnect);

            // Act
            var decoded = ControlMessage.TryDecode(encoded, out var type);

            // Assert
            Assert.IsTrue(decoded);
            Assert.AreEqual(ControlMessageType.Disconnect, type);
        }

        [TestMethod]
        [DataRow(0, DisplayName = "empty")]
        [DataRow(4, DisplayName = "signature only, type byte missing")]
        [DataRow(6, DisplayName = "one byte too long")]
        [DataRow(64, DisplayName = "an ordinary small payload")]
        public void WrongLength_IsRejected(int length)
        {
            // Arrange
            var buffer = new byte[length];
            SignatureBytes.AsSpan()[..Math.Min(4, length)].CopyTo(buffer);

            // Act
            var decoded = ControlMessage.TryDecode(buffer, out _);

            // Assert
            Assert.IsFalse(decoded, $"A {length}-byte buffer was accepted as a control message.");
        }

        [TestMethod]
        public void WrongSignature_IsRejected()
        {
            // Arrange
            byte[] userPayload = [0x00, 0x01, 0x02, 0x03, 0x01];

            // Act
            var decoded = ControlMessage.TryDecode(userPayload, out _);

            // Assert
            Assert.IsFalse(decoded);
        }

        [TestMethod]
        public void SignatureInWrongByteOrder_IsRejected()
        {
            // Arrange
            byte[] bigEndian = [0x50, 0x4D, 0x4E, 0x43, 0x01];

            // Act
            var decoded = ControlMessage.TryDecode(bigEndian, out _);

            // Assert
            Assert.IsFalse(decoded);
        }

        [TestMethod]
        public void UndefinedType_DecodesSuccessfully_ForForwardCompatibility()
        {
            // Arrange
            byte[] futureType = [.. SignatureBytes, 0xFE];

            // Act
            var decoded = ControlMessage.TryDecode(futureType, out var type);

            // Assert
            Assert.IsTrue(decoded);
            Assert.AreEqual((ControlMessageType)0xFE, type);
            Assert.IsFalse(Enum.IsDefined(type), "0xFE should not be a defined type in this build.");
        }

        [TestMethod]
        public void RejectedMessage_LeavesTypeAtDefault()
        {
            // Arrange
            byte[] notAControlMessage = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF];

            // Act
            ControlMessage.TryDecode(notAControlMessage, out var type);

            // Assert
            Assert.AreEqual(default, type);
        }
    }
}
