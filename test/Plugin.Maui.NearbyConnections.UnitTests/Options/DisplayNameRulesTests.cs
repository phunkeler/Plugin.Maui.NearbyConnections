using System.Globalization;
using System.Text;

namespace Plugin.Maui.NearbyConnections.UnitTests;

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

        [Fact]
        public void MultiByteNameWithinTheByteLimit_IsAccepted()
        {
            // Arrange
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

        [Fact]
        public void ShortMultiByteNameOverTheByteLimit_IsRejected()
        {
            // Arrange
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
