namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Covers the control-message wire format.
/// </summary>
/// <remarks>
/// <para>
/// This is a <b>wire format</b>: both ends of a connection must agree on it, and the two ends can
/// be running different versions of the app. A change that breaks compatibility produces no compile
/// error and no runtime exception — a control message is simply misread as a payload, or a payload
/// is misread as a control message, and the connection behaves strangely for reasons nothing logs.
/// </para>
/// <para>
/// The byte-level assertions here are therefore deliberate. They pin the exact encoding rather than
/// round-tripping through the same code that produced it, so a change to the signature, the byte
/// order, or the layout fails loudly instead of silently shipping an incompatible build.
/// </para>
/// </remarks>
[TestCategory("Connections")]
public class ControlMessageTests
{
    /// <summary>
    /// The signature bytes, little-endian, spelling "PMNC" when read as ASCII.
    /// Hard-coded rather than derived from the implementation constant: a test that reads the
    /// constant would happily follow it to a new value and prove nothing.
    /// </summary>
    static readonly byte[] s_signatureBytes = [0x43, 0x4E, 0x4D, 0x50];

    [TestClass]
    public sealed class Encode : ControlMessageTests
    {
        [TestMethod]
        public void Disconnect_ProducesTheExactExpectedBytes()
        {
            var encoded = ControlMessage.Encode(ControlMessageType.Disconnect);

            Assert.HasCount(5, encoded, "The frame is a 4-byte signature plus a 1-byte type.");
            Assert.AreSequenceEqual(
                s_signatureBytes, encoded[..4], "Signature bytes changed — every peer on the previous version will stop recognising control messages.");
            Assert.AreEqual(0x01, encoded[4], "Disconnect's wire value changed.");
        }

        [TestMethod]
        public void SignatureOnTheWire_IsPmncReversed()
        {
            // The constant 0x504D4E43 spells "PMNC" when read as a big-endian integer, which is
            // what the source comment describes. It is written little-endian, so the bytes that
            // actually travel are reversed: "CNMP". Both facts are true and the difference is easy
            // to trip over — a maintainer "fixing" the apparent typo by switching to
            // WriteUInt32BigEndian would break compatibility with every deployed peer.
            var encoded = ControlMessage.Encode(ControlMessageType.Disconnect);

            Assert.AreEqual("CNMP", System.Text.Encoding.ASCII.GetString(encoded[..4]));
        }
    }

    [TestClass]
    public sealed class TryDecode : ControlMessageTests
    {
        [TestMethod]
        public void EncodedMessage_RoundTrips()
        {
            var encoded = ControlMessage.Encode(ControlMessageType.Disconnect);

            var decoded = ControlMessage.TryDecode(encoded, out var type);

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
            // Length is checked before the signature read, so a short buffer must not throw.
            var buffer = new byte[length];
            s_signatureBytes.AsSpan()[..Math.Min(4, length)].CopyTo(buffer);

            var decoded = ControlMessage.TryDecode(buffer, out _);

            Assert.IsFalse(decoded, $"A {length}-byte buffer was accepted as a control message.");
        }

        [TestMethod]
        public void WrongSignature_IsRejected()
        {
            // The case that matters most in practice: an ordinary 5-byte user payload must not be
            // mistaken for a control message and swallowed instead of delivered.
            byte[] userPayload = [0x00, 0x01, 0x02, 0x03, 0x01];

            var decoded = ControlMessage.TryDecode(userPayload, out _);

            Assert.IsFalse(decoded);
        }

        [TestMethod]
        public void SignatureInWrongByteOrder_IsRejected()
        {
            // Big-endian "PMNC". Guards against someone "fixing" the endianness on one side only.
            byte[] bigEndian = [0x50, 0x4D, 0x4E, 0x43, 0x01];

            var decoded = ControlMessage.TryDecode(bigEndian, out _);

            Assert.IsFalse(decoded);
        }

        [TestMethod]
        public void UndefinedType_DecodesSuccessfully_ForForwardCompatibility()
        {
            // Deliberate: a future version may add control types this build does not know. Decoding
            // succeeds and the caller logs an unknown type (see HandleControlMessage) rather than
            // treating the frame as user data. Rejecting here would deliver a control frame to the
            // app as a payload, which is worse.
            byte[] futureType = [.. s_signatureBytes, 0xFE];

            var decoded = ControlMessage.TryDecode(futureType, out var type);

            Assert.IsTrue(decoded);
            Assert.AreEqual((ControlMessageType)0xFE, type);
            Assert.IsFalse(Enum.IsDefined(type), "0xFE should not be a defined type in this build.");
        }

        [TestMethod]
        public void RejectedMessage_LeavesTypeAtDefault()
        {
            byte[] notAControlMessage = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF];

            ControlMessage.TryDecode(notAControlMessage, out var type);

            Assert.AreEqual(default, type);
        }
    }
}
