using System.Globalization;
using System.Text;

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Covers the <c>DisplayName</c> rules Apple enforces for an <c>MCPeerID</c>.
/// </summary>
/// <remarks>
/// These rules are a crash guard, not a style check: an empty or over-long value makes
/// <c>MCPeerID</c>'s native initializer raise an <c>NSInvalidArgumentException</c>, which reaches
/// the app as a fatal native crash that no <c>try</c>/<c>catch</c> can intercept. The default value
/// is <c>DeviceInfo.Name</c>, so the limit is reachable with no consumer mistake at all — a rule
/// that silently stops being enforced re-opens that crash for real users.
/// </remarks>
[Trait("Category", "Options")]
public class DisplayNameRulesTests
{
    public sealed class Accepts : DisplayNameRulesTests
    {
        [Theory]
        [InlineData("Sam's iPhone", TestDisplayName = "an ordinary device name")]
        [InlineData("A", TestDisplayName = "single character, the shortest legal value")]
        public void ValidDisplayName_ProducesNoFailures(string displayName)
        {
            // Arrange
            var failures = new List<string>();

            // Act
            DisplayNameRules.Validate(displayName, failures);

            // Assert
            // A legal display name must not be rejected.
            Assert.Empty(failures);
        }

        [Fact]
        public void NameOfExactlyTheByteLimit_IsAccepted()
        {
            // Arrange
            var displayName = new string('A', DisplayNameRules.MaxBytes);
            var failures = new List<string>();

            // Act
            DisplayNameRules.Validate(displayName, failures);

            // Assert
            Assert.Empty(failures);
        }

        // The limit is bytes, so a multi-byte name is legal at far fewer characters. This is the
        // boundary a character-based check would wrongly reject.
        [Fact]
        public void MultiByteNameWithinTheByteLimit_IsAccepted()
        {
            // Arrange — 3 UTF-8 bytes per character, so 21 characters is 63 bytes.
            var displayName = new string('あ', DisplayNameRules.MaxBytes / 3);
            var failures = new List<string>();

            // Act
            DisplayNameRules.Validate(displayName, failures);

            // Assert
            Assert.Equal(DisplayNameRules.MaxBytes, Encoding.UTF8.GetByteCount(displayName));
            Assert.Empty(failures);
        }
    }

    public sealed class Rejects : DisplayNameRulesTests
    {
        [Fact]
        public void NullName_IsRejected()
        {
            // Arrange
            var failures = new List<string>();

            // Act
            DisplayNameRules.Validate(null, failures);

            // Assert
            Assert.Single(failures);
        }

        [Fact]
        public void EmptyName_IsRejected()
        {
            // Arrange
            var failures = new List<string>();

            // Act
            DisplayNameRules.Validate(string.Empty, failures);

            // Assert
            Assert.Single(failures);
        }

        [Fact]
        public void NameOneByteOverTheLimit_IsRejected()
        {
            // Arrange
            var displayName = new string('A', DisplayNameRules.MaxBytes + 1);
            var failures = new List<string>();

            // Act
            DisplayNameRules.Validate(displayName, failures);

            // Assert
            Assert.Single(failures);
        }

        // The case that reaches a real user: a device named in a non-Latin script is well under any
        // plausible character limit while being over the byte limit.
        [Fact]
        public void ShortMultiByteNameOverTheByteLimit_IsRejected()
        {
            // Arrange — 22 characters, but 66 UTF-8 bytes.
            var displayName = new string('あ', 22);
            var failures = new List<string>();

            // Act
            DisplayNameRules.Validate(displayName, failures);

            // Assert
            Assert.True(Encoding.UTF8.GetByteCount(displayName) > DisplayNameRules.MaxBytes);
            Assert.Single(failures);
        }

        [Fact]
        public void RejectionMessage_NamesTheByteCountAndTheLimit()
        {
            // Arrange
            var displayName = new string('A', DisplayNameRules.MaxBytes + 1);
            var failures = new List<string>();

            // Act
            DisplayNameRules.Validate(displayName, failures);

            // Assert
            Assert.Contains($"{DisplayNameRules.MaxBytes + 1} UTF-8 bytes", failures[0]);
            Assert.Contains(DisplayNameRules.MaxBytes.ToString(CultureInfo.InvariantCulture), failures[0]);
        }
    }
}
