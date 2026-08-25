namespace Plugin.Maui.NearbyConnections.UnitTests;

[Trait("Category", "Connections")]
public class PeerLookupTests
{
    readonly PeerLookup _sut;

    public PeerLookupTests()
    {
        _sut = new PeerLookup();
    }

    public sealed class Record : PeerLookupTests
    {
        [Fact]
        public void NewPeer_ReturnsDeviceWithKeyAndDisplayName()
        {
            // Arrange
            var key = "peer-1";
            var displayName = "Alice";

            // Act
            var device = _sut.Record(key, displayName);

            // Assert
            Assert.Equal(key, device.Id);
            Assert.Equal(displayName, device.DisplayName);
        }

        [Fact]
        public void ExistingPeer_ReturnsSameDeviceInstance()
        {
            // Arrange
            var key = "peer-1";
            var displayName = "Alice";
            var first = _sut.Record(key, displayName);

            // Act
            var second = _sut.Record(key, displayName);

            // Assert
            Assert.Same(first, second);
        }

        [Fact]
        public void ExistingPeer_DoesNotAdoptNewDisplayName()
        {
            // Arrange — a rediscovery re-records the same endpoint; the incumbent must survive.
            var key = "peer-1";
            var original = _sut.Record(key, "Alice");

            // Act
            var rediscovered = _sut.Record(key, "Alice (renamed)");

            // Assert
            Assert.Same(original, rediscovered);
            Assert.Equal("Alice", rediscovered.DisplayName);
        }
    }

    public sealed class Sanitize : PeerLookupTests
    {
        [Fact]
        public void PlainName_IsUnchanged()
        {
            // Arrange
            var displayName = "Alice's iPhone";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.Equal(displayName, device.DisplayName);
        }

        [Fact]
        public void NameWithNewlines_HasControlCharactersRemoved()
        {
            // Arrange — a forged log record appended to an attacker-chosen name.
            var displayName = "Alice\r\n[ERROR] transfer approved";
            var expected = "Alice[ERROR] transfer approved";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.Equal(expected, device.DisplayName);
        }

        [Fact]
        public void OverlongName_IsTruncatedToMaxLength()
        {
            // Arrange — ASCII, so one UTF-8 byte per character and the cap lands on a round number.
            var displayName = new string('A', PeerLookup.MaxDisplayNameBytes + 50);

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.Equal(PeerLookup.MaxDisplayNameBytes, device.DisplayName!.Length);
        }

        // The cap counts UTF-8 bytes, which is what both a log sink and the platforms constrain. A
        // multi-byte name therefore keeps fewer characters than an ASCII one.
        [Fact]
        public void OverlongMultiByteName_IsTruncatedByBytesNotCharacters()
        {
            // Arrange — U+00E9 is one char but two UTF-8 bytes.
            var displayName = new string('é', PeerLookup.MaxDisplayNameBytes);
            var expectedChars = PeerLookup.MaxDisplayNameBytes / 2;

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.Equal(expectedChars, device.DisplayName!.Length);
            Assert.Equal(
                PeerLookup.MaxDisplayNameBytes,
                System.Text.Encoding.UTF8.GetByteCount(device.DisplayName));
        }

        [Fact]
        public void NameOfOnlyControlCharacters_BecomesNull()
        {
            // Arrange
            var displayName = "\r\n\t\u0000";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.Null(device.DisplayName);
        }

        [Fact]
        public void NullName_StaysNull()
        {
            // Arrange
            string? displayName = null;

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.Null(device.DisplayName);
        }

        [Fact]
        public void TruncationDoesNotSplitSurrogatePair()
        {
            // Arrange — every rune is 4 UTF-8 bytes and 2 UTF-16 units, so the cap lands on a pair
            // boundary only if whole runes are appended.
            var displayName = string.Concat(Enumerable.Repeat("\U0001F600", PeerLookup.MaxDisplayNameBytes));
            var expectedChars = PeerLookup.MaxDisplayNameBytes / 4 * 2;

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert — a split pair leaves a lone high surrogate at the end.
            Assert.False(char.IsHighSurrogate(device.DisplayName![^1]));
            Assert.Equal(expectedChars, device.DisplayName.Length);
        }

        [Fact]
        public void EmptyName_BecomesNull()
        {
            // Arrange — an empty name and an all-control name both mean "no usable name", so both
            // must reach a consumer as null rather than as two different representations.
            var displayName = string.Empty;

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.Null(device.DisplayName);
        }

        // U+2028 and U+2029 are LineSeparator and ParagraphSeparator, not Control, so Rune.IsControl
        // let them through. Both break lines in common log formatters, which forges a whole record
        // around the real one — the attack stripping \r\n was meant to stop.
        [Fact]
        public void NameWithUnicodeLineSeparators_HasThemRemoved()
        {
            // Arrange
            var displayName = "Alice\u2028warn: pairing approved\u2029by user";
            var expected = "Alicewarn: pairing approvedby user";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.Equal(expected, device.DisplayName);
        }

        // A bidirectional override reverses how the remainder of the name renders, so a peer can
        // make one string display as another to the person deciding whether to trust the device.
        [Fact]
        public void NameWithBidiOverride_HasItRemoved()
        {
            // Arrange
            var displayName = "photo\u202Egnp.exe";
            var expected = "photognp.exe";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.Equal(expected, device.DisplayName);
        }

        // Zero-width characters let two distinct peers render identically in the device list.
        [Fact]
        public void NameWithZeroWidthCharacters_HasThemRemoved()
        {
            // Arrange
            var displayName = "Ali\u200Bce\u200D\uFEFF";
            var expected = "Alice";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.Equal(expected, device.DisplayName);
        }

        [Fact]
        public void TwoNamesDifferingOnlyByZeroWidth_SanitizeToTheSameString()
        {
            // Arrange
            var impostor = _sut.Record("peer-1", "Ali\u200Bce");

            // Act
            var genuine = _sut.Record("peer-2", "Alice");

            // Assert — identical rendering is now visible as identical data, rather than hiding
            // behind two strings that look the same but compare unequal.
            Assert.Equal(genuine.DisplayName, impostor.DisplayName);
        }

        [Fact]
        public void NameOfOnlyRejectedCharacters_BecomesNull()
        {
            // Arrange
            var displayName = "\u202E\u200B\u2028\uFEFF";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.Null(device.DisplayName);
        }

        [Fact]
        public void NameWithLoneSurrogate_HasItRemoved()
        {
            // Arrange — an unpaired high surrogate renders unpredictably per platform.
            var displayName = "ok\uD800end";
            var expected = "okend";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.Equal(expected, device.DisplayName);
        }

        [Fact]
        public void NameWithLegitimateNonAsciiText_IsUnchanged()
        {
            // Arrange — the filter must not reject ordinary international names or emoji.
            var displayName = "田中さんの iPhone \U0001F600";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.Equal(displayName, device.DisplayName);
        }
    }

    public sealed class TryGetDevice : PeerLookupTests
    {
        [Fact]
        public void KnownKey_ReturnsTrueAndDevice()
        {
            // Arrange
            var key = "peer-1";
            _sut.Record(key, "Alice");

            // Act
            var found = _sut.TryGetDevice(key, out var device);

            // Assert
            Assert.True(found);
            Assert.NotNull(device);
            Assert.Equal(key, device.Id);
        }

        [Fact]
        public void UnknownKey_ReturnsFalseAndNullOut()
        {
            // Arrange
            var key = "peer-unknown";

            // Act
            var found = _sut.TryGetDevice(key, out var device);

            // Assert
            Assert.False(found);
            Assert.Null(device);
        }
    }

    public sealed class Remove : PeerLookupTests
    {
        [Fact]
        public void KnownKey_ReturnsRemovedDevice()
        {
            // Arrange
            var key = "peer-1";
            _sut.Record(key, "Alice");

            // Act
            var removed = _sut.Remove(key);

            // Assert
            Assert.NotNull(removed);
            Assert.Equal(key, removed.Id);
        }

        [Fact]
        public void KnownKey_IsNoLongerResolvable()
        {
            // Arrange
            var key = "peer-1";
            _sut.Record(key, "Alice");

            // Act
            _sut.Remove(key);

            // Assert
            Assert.False(_sut.TryGetDevice(key, out _));
        }

        [Fact]
        public void UnknownKey_ReturnsNull()
        {
            // Arrange
            var key = "peer-unknown";

            // Act
            var removed = _sut.Remove(key);

            // Assert
            Assert.Null(removed);
        }
    }

    public sealed class Clear : PeerLookupTests
    {
        [Fact]
        public void RemovesAllTrackedPeers()
        {
            // Arrange
            _sut.Record("peer-1", "Alice");
            _sut.Record("peer-2", "Bob");

            // Act
            _sut.Clear();

            // Assert
            Assert.False(_sut.TryGetDevice("peer-1", out _));
            Assert.False(_sut.TryGetDevice("peer-2", out _));
        }

        [Fact]
        public void OnEmptyRegistry_DoesNotThrow()
        {
            // Arrange — registry is created empty in constructor

            // Act
            _sut.Clear();

            // Assert
            Assert.False(_sut.TryGetDevice("peer-1", out _));
        }
    }
}
