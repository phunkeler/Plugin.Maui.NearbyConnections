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

    public sealed class StreamNameFrame : ControlMessageTests
    {
        [Fact]
        public void RoundTrips_PayloadIdAndName()
        {
            // Arrange
            var frame = ControlMessage.EncodeStreamName(payloadId: 42, "vitals.live");

            // Act
            var recognized = ControlMessage.TryDecode(frame, out var type);
            var decoded = ControlMessage.TryDecodeStreamName(frame, out var payloadId, out var name);

            // Assert
            Assert.True(recognized);
            Assert.Equal(ControlMessageType.StreamName, type);
            Assert.True(decoded);
            Assert.Equal(42, payloadId);
            Assert.Equal("vitals.live", name);
        }

        [Fact]
        public void RoundTrips_NonAsciiName()
        {
            // Arrange
            var frame = ControlMessage.EncodeStreamName(payloadId: 7, "café-Δ");

            // Act
            ControlMessage.TryDecodeStreamName(frame, out _, out var name);

            // Assert
            Assert.Equal("café-Δ", name);
        }

        [Fact]
        public void Decode_StripsControlCharactersFromAPeerSuppliedName()
        {
            // The name is attacker-chosen and reaches log sinks and consumer UI, so it runs through
            // the same filter a display name does.

            // Arrange
            var frame = ControlMessage.EncodeStreamName(payloadId: 3, "vitals‮live");

            // Act
            ControlMessage.TryDecodeStreamName(frame, out _, out var name);

            // Assert
            Assert.Equal("vitalslive", name);
        }

        [Fact]
        public void Encode_RejectsANameOverTheWireLimit()
        {
            // Arrange
            var oversized = new string('x', ControlMessage.MaxStreamNameBytes + 1);

            // Act
            void Act() => ControlMessage.EncodeStreamName(payloadId: 1, oversized);

            // Assert
            Assert.Throws<ArgumentException>(Act);
        }

        [Fact]
        public void TryDecodeStreamName_FailsSoftOnATruncatedBody()
        {
            // Arrange
            var frame = ControlMessage.EncodeStreamName(payloadId: 42, "vitals.live");
            var truncated = frame.AsSpan(0, frame.Length - 3);

            // Act
            var decoded = ControlMessage.TryDecodeStreamName(truncated, out _, out var name);

            // Assert
            Assert.False(decoded);
            Assert.Null(name);
        }

        [Fact]
        public void DisconnectFrame_KeepsItsFiveByteLayout()
        {
            // The header is the cross-version wire contract: the Disconnect frame an older peer
            // sends must keep decoding, and its layout must never grow.

            // Arrange
            var frame = ControlMessage.Encode(ControlMessageType.Disconnect);

            // Act
            var recognized = ControlMessage.TryDecode(frame, out var type);

            // Assert
            Assert.Equal(5, frame.Length);
            Assert.True(recognized);
            Assert.Equal(ControlMessageType.Disconnect, type);
        }
    }

    public sealed class ConnectRequestFrame : ControlMessageTests
    {
        [Fact]
        public void RoundTrips_WindowAndName()
        {
            // Arrange — the Android shape: the frame re-carries the display name.
            var frame = ControlMessage.EncodeConnectRequest(TimeSpan.FromSeconds(30), "Alice");

            // Act
            var recognized = ControlMessage.TryDecode(frame, out var type);
            var decoded = ControlMessage.TryDecodeConnectRequest(frame, out var window, out var name);

            // Assert
            Assert.True(recognized);
            Assert.Equal(ControlMessageType.ConnectRequest, type);
            Assert.True(decoded);
            Assert.Equal(TimeSpan.FromSeconds(30), window);
            Assert.Equal("Alice", name);
        }

        [Fact]
        public void RoundTrips_TheEmptyName()
        {
            // The iOS shape: the name rides MCPeerID natively, so the context is the 9-byte
            // window-only frame.

            // Arrange
            var frame = ControlMessage.EncodeConnectRequest(TimeSpan.FromSeconds(10), displayName: string.Empty);

            // Act
            var decoded = ControlMessage.TryDecodeConnectRequest(frame, out var window, out var name);

            // Assert
            Assert.Equal(9, frame.Length);
            Assert.True(decoded);
            Assert.Equal(TimeSpan.FromSeconds(10), window);
            Assert.Equal(string.Empty, name);
        }

        [Fact]
        public void InfiniteWindow_RoundTripsThroughTheSentinel()
        {
            // Arrange
            var frame = ControlMessage.EncodeConnectRequest(Timeout.InfiniteTimeSpan, string.Empty);

            // Act
            var decoded = ControlMessage.TryDecodeConnectRequest(frame, out var window, out _);

            // Assert
            Assert.True(decoded);
            Assert.Equal(Timeout.InfiniteTimeSpan, window);
        }

        [Fact]
        public void FiniteWindowAboveTheFieldRange_SaturatesBelowTheSentinel()
        {
            // Only Timeout.InfiniteTimeSpan gets the sentinel: a huge finite duration must not be
            // mistaken for an unbounded declaration.

            // Arrange
            var frame = ControlMessage.EncodeConnectRequest(TimeSpan.FromDays(365), string.Empty);

            // Act
            var decoded = ControlMessage.TryDecodeConnectRequest(frame, out var window, out _);

            // Assert
            Assert.True(decoded);
            Assert.NotEqual(Timeout.InfiniteTimeSpan, window);
            Assert.Equal(TimeSpan.FromMilliseconds(0xFFFFFFFE), window);
        }

        [Fact]
        public void ZeroWindow_IsValidAndDecodesToZero()
        {
            // A degenerate declared window is honored — it lands already-expired on the receiver
            // and only hurts the sender.

            // Arrange
            var frame = ControlMessage.EncodeConnectRequest(TimeSpan.Zero, string.Empty);

            // Act
            var decoded = ControlMessage.TryDecodeConnectRequest(frame, out var window, out _);

            // Assert
            Assert.True(decoded);
            Assert.Equal(TimeSpan.Zero, window);
        }

        [Fact]
        public void Encode_RejectsANameOverTheWireBudget()
        {
            // 9 bytes of frame overhead + a 63-byte name is what keeps the whole frame inside
            // Google's ~131-byte pre-connection cap.

            // Arrange
            var oversized = new string('x', DisplayNameRules.MaxBytes + 1);

            // Act
            void Act() => ControlMessage.EncodeConnectRequest(TimeSpan.FromSeconds(30), oversized);

            // Assert
            Assert.Throws<ArgumentException>(Act);
        }

        [Fact]
        public void TruncatedFrame_IsRejected()
        {
            // Arrange — cut into the window field.
            var frame = ControlMessage.EncodeConnectRequest(TimeSpan.FromSeconds(30), string.Empty);
            var truncated = frame.AsSpan(0, frame.Length - 2);

            // Act
            var recognized = ControlMessage.TryDecode(truncated, out _);
            var decoded = ControlMessage.TryDecodeConnectRequest(truncated, out _, out var name);

            // Assert
            Assert.False(recognized);
            Assert.False(decoded);
            Assert.Null(name);
        }

        [Fact]
        public void LegacyRawName_IsNotMistakenForAFrame()
        {
            // A legacy Android peer's endpointInfo is its raw UTF-8 display name. It must fail the
            // decode so the receiver falls back to name-as-string plus the default window.

            // Arrange
            var legacy = System.Text.Encoding.UTF8.GetBytes("Alice's Phone");

            // Act
            var decoded = ControlMessage.TryDecodeConnectRequest(legacy, out _, out var name);

            // Assert
            Assert.False(decoded);
            Assert.Null(name);
        }

        [Fact]
        public void AppPayloadStartingWithTheSignature_IsSwallowedAsAControlFrame()
        {
            // The false-positive case, pinned: an application payload that starts with the
            // signature, the 0x03 type byte, and at least four more bytes decodes as a control
            // frame and is not delivered as an application payload. Same accepted cross-version
            // behavior class as the other frame types.

            // Arrange
            byte[] payload = [.. SignatureBytes, 0x03, 0xDE, 0xAD, 0xBE, 0xEF];

            // Act
            var recognized = ControlMessage.TryDecode(payload, out var type);

            // Assert
            Assert.True(recognized);
            Assert.Equal(ControlMessageType.ConnectRequest, type);
        }
    }
}
