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
            // Arrange — a newline breaks the layout of the row the name is drawn in.
            var displayName = "Alice\r\n\tPhone";
            var expected = "AlicePhone";

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

        // ZWNJ is spelling in Persian: it selects the correct word-internal letter form. Stripping
        // it corrupts an ordinary Iranian device name.
        [Fact]
        public void PersianNameWithZeroWidthNonJoiner_KeepsIt()
        {
            // Arrange — "می‌خواهم", whose ZWNJ separates the mi- prefix from the verb stem.
            var displayName = "می‌خواهم";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.Equal(displayName, device.DisplayName);
        }

        // ZWNJ suppresses a conjunct ligature in the Indic scripts, which changes which consonant
        // cluster renders — a spelling difference, not a cosmetic one.
        [Fact]
        public void DevanagariNameWithZeroWidthNonJoiner_KeepsIt()
        {
            // Arrange
            var displayName = "क्‍ष";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.Equal(displayName, device.DisplayName);
        }

        [Fact]
        public void EmojiNameJoinedByZeroWidthJoiner_KeepsTheJoiners()
        {
            // Arrange — a family emoji is three people joined by ZWJ; without them it renders as
            // three separate glyphs.
            var displayName = "👨‍👩‍👧";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.Equal(displayName, device.DisplayName);
        }

        [Fact]
        public void ChineseName_IsUnchanged()
        {
            // Arrange — Han characters are unaffected by the joiner rules, and must stay intact.
            var displayName = "小明的手机";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.Equal(displayName, device.DisplayName);
        }

        // Keeping the joiners must not readmit the rest of Format. An override rewrites how
        // surrounding text renders, which no ordinary letter does.
        [Fact]
        public void NameWithJoinerAndBidiOverride_KeepsOnlyTheJoiner()
        {
            // Arrange
            var displayName = "می‌خواهم‮gnp.exe";
            var expected = "می‌خواهمgnp.exe";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.Equal(expected, device.DisplayName);
        }

        [Fact]
        public void NameOfOnlyRejectedCharacters_BecomesNull()
        {
            // Arrange
            var displayName = "\u202E\u0000\u0009\u009F";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.Null(device.DisplayName);
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
