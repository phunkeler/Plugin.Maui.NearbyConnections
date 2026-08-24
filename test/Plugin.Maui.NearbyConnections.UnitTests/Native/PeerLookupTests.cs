namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestCategory("Connections")]
public class PeerLookupTests
{
    readonly PeerLookup _sut;

    public PeerLookupTests()
    {
        _sut = new PeerLookup();
    }

    [TestClass]
    public sealed class Record : PeerLookupTests
    {
        [TestMethod]
        public void NewPeer_ReturnsDeviceWithKeyAndDisplayName()
        {
            // Arrange
            var key = "peer-1";
            var displayName = "Alice";

            // Act
            var device = _sut.Record(key, displayName);

            // Assert
            Assert.AreEqual(key, device.Id);
            Assert.AreEqual(displayName, device.DisplayName);
        }

        [TestMethod]
        public void ExistingPeer_ReturnsSameDeviceInstance()
        {
            // Arrange
            var key = "peer-1";
            var displayName = "Alice";
            var first = _sut.Record(key, displayName);

            // Act
            var second = _sut.Record(key, displayName);

            // Assert
            Assert.AreSame(first, second);
        }

        [TestMethod]
        public void ExistingPeer_DoesNotAdoptNewDisplayName()
        {
            // Arrange — a rediscovery re-records the same endpoint; the incumbent must survive.
            var key = "peer-1";
            var original = _sut.Record(key, "Alice");

            // Act
            var rediscovered = _sut.Record(key, "Alice (renamed)");

            // Assert
            Assert.AreSame(original, rediscovered);
            Assert.AreEqual("Alice", rediscovered.DisplayName);
        }
    }

    [TestClass]
    public sealed class Sanitize : PeerLookupTests
    {
        [TestMethod]
        public void PlainName_IsUnchanged()
        {
            // Arrange
            var displayName = "Alice's iPhone";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.AreEqual(displayName, device.DisplayName);
        }

        [TestMethod]
        public void NameWithNewlines_HasControlCharactersRemoved()
        {
            // Arrange — a forged log record appended to an attacker-chosen name.
            var displayName = "Alice\r\n[ERROR] transfer approved";
            var expected = "Alice[ERROR] transfer approved";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.AreEqual(expected, device.DisplayName);
        }

        [TestMethod]
        public void OverlongName_IsTruncatedToMaxLength()
        {
            // Arrange — ASCII, so one UTF-8 byte per character and the cap lands on a round number.
            var displayName = new string('A', PeerLookup.MaxDisplayNameBytes + 50);

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.AreEqual(PeerLookup.MaxDisplayNameBytes, device.DisplayName!.Length);
        }

        // The cap counts UTF-8 bytes, which is what both a log sink and the platforms constrain. A
        // multi-byte name therefore keeps fewer characters than an ASCII one.
        [TestMethod]
        public void OverlongMultiByteName_IsTruncatedByBytesNotCharacters()
        {
            // Arrange — U+00E9 is one char but two UTF-8 bytes.
            var displayName = new string('é', PeerLookup.MaxDisplayNameBytes);
            var expectedChars = PeerLookup.MaxDisplayNameBytes / 2;

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.AreEqual(expectedChars, device.DisplayName!.Length);
            Assert.AreEqual(
                PeerLookup.MaxDisplayNameBytes,
                System.Text.Encoding.UTF8.GetByteCount(device.DisplayName));
        }

        [TestMethod]
        public void NameOfOnlyControlCharacters_BecomesNull()
        {
            // Arrange
            var displayName = "\r\n\t\u0000";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.IsNull(device.DisplayName);
        }

        [TestMethod]
        public void NullName_StaysNull()
        {
            // Arrange
            string? displayName = null;

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.IsNull(device.DisplayName);
        }

        [TestMethod]
        public void TruncationDoesNotSplitSurrogatePair()
        {
            // Arrange — every rune is 4 UTF-8 bytes and 2 UTF-16 units, so the cap lands on a pair
            // boundary only if whole runes are appended.
            var displayName = string.Concat(Enumerable.Repeat("\U0001F600", PeerLookup.MaxDisplayNameBytes));
            var expectedChars = PeerLookup.MaxDisplayNameBytes / 4 * 2;

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert — a split pair leaves a lone high surrogate at the end.
            Assert.IsFalse(char.IsHighSurrogate(device.DisplayName![^1]));
            Assert.AreEqual(expectedChars, device.DisplayName.Length);
        }

        [TestMethod]
        public void EmptyName_BecomesNull()
        {
            // Arrange — an empty name and an all-control name both mean "no usable name", so both
            // must reach a consumer as null rather than as two different representations.
            var displayName = string.Empty;

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.IsNull(device.DisplayName);
        }

        // U+2028 and U+2029 are LineSeparator and ParagraphSeparator, not Control, so Rune.IsControl
        // let them through. Both break lines in common log formatters, which forges a whole record
        // around the real one — the attack stripping \r\n was meant to stop.
        [TestMethod]
        public void NameWithUnicodeLineSeparators_HasThemRemoved()
        {
            // Arrange
            var displayName = "Alice\u2028warn: pairing approved\u2029by user";
            var expected = "Alicewarn: pairing approvedby user";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.AreEqual(expected, device.DisplayName);
        }

        // A bidirectional override reverses how the remainder of the name renders, so a peer can
        // make one string display as another to the person deciding whether to trust the device.
        [TestMethod]
        public void NameWithBidiOverride_HasItRemoved()
        {
            // Arrange
            var displayName = "photo\u202Egnp.exe";
            var expected = "photognp.exe";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.AreEqual(expected, device.DisplayName);
        }

        // Zero-width characters let two distinct peers render identically in the device list.
        [TestMethod]
        public void NameWithZeroWidthCharacters_HasThemRemoved()
        {
            // Arrange
            var displayName = "Ali\u200Bce\u200D\uFEFF";
            var expected = "Alice";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.AreEqual(expected, device.DisplayName);
        }

        [TestMethod]
        public void TwoNamesDifferingOnlyByZeroWidth_SanitizeToTheSameString()
        {
            // Arrange
            var impostor = _sut.Record("peer-1", "Ali\u200Bce");

            // Act
            var genuine = _sut.Record("peer-2", "Alice");

            // Assert — identical rendering is now visible as identical data, rather than hiding
            // behind two strings that look the same but compare unequal.
            Assert.AreEqual(genuine.DisplayName, impostor.DisplayName);
        }

        [TestMethod]
        public void NameOfOnlyRejectedCharacters_BecomesNull()
        {
            // Arrange
            var displayName = "\u202E\u200B\u2028\uFEFF";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.IsNull(device.DisplayName);
        }

        [TestMethod]
        public void NameWithLoneSurrogate_HasItRemoved()
        {
            // Arrange — an unpaired high surrogate renders unpredictably per platform.
            var displayName = "ok\uD800end";
            var expected = "okend";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.AreEqual(expected, device.DisplayName);
        }

        [TestMethod]
        public void NameWithLegitimateNonAsciiText_IsUnchanged()
        {
            // Arrange — the filter must not reject ordinary international names or emoji.
            var displayName = "田中さんの iPhone \U0001F600";

            // Act
            var device = _sut.Record("peer-1", displayName);

            // Assert
            Assert.AreEqual(displayName, device.DisplayName);
        }
    }

    [TestClass]
    public sealed class TryGetDevice : PeerLookupTests
    {
        [TestMethod]
        public void KnownKey_ReturnsTrueAndDevice()
        {
            // Arrange
            var key = "peer-1";
            _sut.Record(key, "Alice");

            // Act
            var found = _sut.TryGetDevice(key, out var device);

            // Assert
            Assert.IsTrue(found);
            Assert.IsNotNull(device);
            Assert.AreEqual(key, device.Id);
        }

        [TestMethod]
        public void UnknownKey_ReturnsFalseAndNullOut()
        {
            // Arrange
            var key = "peer-unknown";

            // Act
            var found = _sut.TryGetDevice(key, out var device);

            // Assert
            Assert.IsFalse(found);
            Assert.IsNull(device);
        }
    }

    [TestClass]
    public sealed class Remove : PeerLookupTests
    {
        [TestMethod]
        public void KnownKey_ReturnsRemovedDevice()
        {
            // Arrange
            var key = "peer-1";
            _sut.Record(key, "Alice");

            // Act
            var removed = _sut.Remove(key);

            // Assert
            Assert.IsNotNull(removed);
            Assert.AreEqual(key, removed.Id);
        }

        [TestMethod]
        public void KnownKey_IsNoLongerResolvable()
        {
            // Arrange
            var key = "peer-1";
            _sut.Record(key, "Alice");

            // Act
            _sut.Remove(key);

            // Assert
            Assert.IsFalse(_sut.TryGetDevice(key, out _));
        }

        [TestMethod]
        public void UnknownKey_ReturnsNull()
        {
            // Arrange
            var key = "peer-unknown";

            // Act
            var removed = _sut.Remove(key);

            // Assert
            Assert.IsNull(removed);
        }
    }

    [TestClass]
    public sealed class Clear : PeerLookupTests
    {
        [TestMethod]
        public void RemovesAllTrackedPeers()
        {
            // Arrange
            _sut.Record("peer-1", "Alice");
            _sut.Record("peer-2", "Bob");

            // Act
            _sut.Clear();

            // Assert
            Assert.IsFalse(_sut.TryGetDevice("peer-1", out _));
            Assert.IsFalse(_sut.TryGetDevice("peer-2", out _));
        }

        [TestMethod]
        public void OnEmptyRegistry_DoesNotThrow()
        {
            // Arrange — registry is created empty in constructor

            // Act
            _sut.Clear();

            // Assert
            Assert.IsFalse(_sut.TryGetDevice("peer-1", out _));
        }
    }
}
