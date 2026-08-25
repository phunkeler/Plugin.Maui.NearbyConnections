namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Covers the <c>ServiceId</c> rules Apple enforces for a Multipeer Connectivity
/// <c>serviceType</c>.
/// </summary>
/// <remarks>
/// These rules are a crash guard, not a style check: an invalid value makes
/// <c>MCNearbyServiceAdvertiser</c>'s native initializer raise an <c>NSInvalidArgumentException</c>,
/// which reaches the app as a fatal native crash that no <c>try</c>/<c>catch</c> can intercept. A
/// rule that silently stops being enforced re-opens that crash, so each one is pinned here.
/// </remarks>
[Trait("Category", "Options")]
public class ServiceIdRulesTests
{
    public sealed class Accepts : ServiceIdRulesTests
    {
        [Theory]
        [InlineData("abc-txtchat", TestDisplayName = "Apple's own documented example")]
        [InlineData("nearbychat", TestDisplayName = "letters only")]
        [InlineData("a", TestDisplayName = "single letter, the shortest legal value")]
        [InlineData("chat2", TestDisplayName = "trailing digit")]
        [InlineData("a1-b2-c3", TestDisplayName = "multiple separated hyphens")]
        [InlineData("abcdefghijklmno", TestDisplayName = "exactly 15 characters, the maximum")]
        public void ValidServiceId_ProducesNoFailures(string serviceId)
        {
            // Arrange
            var failures = new List<string>();

            // Act
            ServiceIdRules.Validate(serviceId, suggestion: null, failures);

            // Assert
            // A service id legal per RFC 6335 must not be rejected.
            Assert.Empty(failures);
        }
    }

    public sealed class Rejects : ServiceIdRulesTests
    {
        [Theory]
        [InlineData("abcdefghijklmnop", TestDisplayName = "16 characters, one over the limit")]
        [InlineData("NearbyChat", TestDisplayName = "uppercase letters")]
        [InlineData("nearby_chat", TestDisplayName = "underscore")]
        [InlineData("nearby.chat", TestDisplayName = "dot")]
        [InlineData("nearby chat", TestDisplayName = "space")]
        [InlineData("_nearbychat._tcp", TestDisplayName = "Bonjour service-type form, the likeliest mistake")]
        [InlineData("123", TestDisplayName = "digits only, no ASCII letter")]
        [InlineData("-chat", TestDisplayName = "leading hyphen")]
        [InlineData("chat-", TestDisplayName = "trailing hyphen")]
        [InlineData("near--chat", TestDisplayName = "adjacent hyphens")]
        public void InvalidServiceId_ProducesAtLeastOneFailure(string serviceId)
        {
            // Arrange
            var failures = new List<string>();

            // Act
            ServiceIdRules.Validate(serviceId, suggestion: null, failures);

            // Assert
            // A service id that violates RFC 6335 must be rejected — accepting it would crash on iOS.
            Assert.NotEmpty(failures);
        }

        [Fact]
        public void UnsetSentinel_ReportsOnlyTheUnsetMessage()
        {
            // The sentinel violates several rules at once. Reporting all of them would bury the one
            // thing the developer needs to know: the value was never set.

            // Arrange
            var failures = new List<string>();

            // Act
            ServiceIdRules.Validate(ServiceIdRules.Unset, suggestion: null, failures);

            // Assert
            Assert.Single(failures);
            Assert.Contains("has not been set", failures[0], StringComparison.Ordinal);
        }

        [Fact]
        public void MultipleViolations_AreAllReported()
        {
            // A developer fixing a value should learn every problem at once rather than one per
            // rebuild. "-A-" is too-short-safe but breaks casing, the letter rule's sibling, and
            // both hyphen rules.

            // Arrange
            var failures = new List<string>();

            // Act
            ServiceIdRules.Validate("-A-", suggestion: null, failures);

            // Assert
            Assert.True(failures.Count > 1,
                "Validation stopped at the first violation instead of reporting all of them.");
        }

        [Fact]
        public void EmptyServiceId_DefersToTheSharedNullCheck()
        {
            // The shared validator already reports null/empty. Adding four more rule violations
            // here would bury it.

            // Arrange
            var failures = new List<string>();

            // Act
            ServiceIdRules.Validate(string.Empty, suggestion: null, failures);

            // Assert
            Assert.Empty(failures);
        }
    }

    public sealed class FailureMessages : ServiceIdRulesTests
    {
        [Fact]
        public void NamesTheOffendingCharacters()
        {
            // The message has to be actionable: "invalid" alone sends the developer hunting.

            // Arrange
            var failures = new List<string>();

            // Act
            ServiceIdRules.Validate("Nearby_Chat", suggestion: null, failures);

            // Assert
            var message = string.Join(" ", failures);
            Assert.Contains("N", message, StringComparison.Ordinal);
            Assert.Contains("_", message, StringComparison.Ordinal);
            Assert.Contains("C", message, StringComparison.Ordinal);
        }

        [Fact]
        public void IncludesTheOffendingValue()
        {
            // Arrange
            var failures = new List<string>();

            // Act
            ServiceIdRules.Validate("way-too-long-service-id", suggestion: null, failures);

            // Assert
            Assert.Contains("way-too-long-service-id", string.Join(" ", failures), StringComparison.Ordinal);
        }
    }

    public sealed class Suggests : ServiceIdRulesTests
    {
        [Theory]
        [InlineData("NearbyChat", "nearbychat", TestDisplayName = "lowercased")]
        [InlineData("My App", "my-app", TestDisplayName = "space becomes a hyphen")]
        [InlineData("ACME_Delivery", "acme-delivery", TestDisplayName = "underscore becomes a hyphen")]
        [InlineData("O'Brien & Sons", "o-brien-sons", TestDisplayName = "a run of punctuation collapses to one hyphen")]
        [InlineData("Contoso Field Service", "contoso-field-s", TestDisplayName = "truncated to the 15-character limit")]
        [InlineData("Cafe  Munster", "cafe-munster", TestDisplayName = "repeated separators do not produce adjacent hyphens")]
        public void ApplicationName_DerivesTheExpectedIdentifier(string applicationName, string expected)
        {
            // Arrange
            // (no setup — Suggest is pure)

            // Act
            var suggestion = ServiceIdRules.Suggest(applicationName);

            // Assert
            Assert.Equal(expected, suggestion);
        }

        [Fact]
        public void DerivedIdentifier_PassesTheRulesItIsSuggestedFor()
        {
            // Arrange
            var failures = new List<string>();
            var suggestion = ServiceIdRules.Suggest("Contoso Field Service");

            // Act
            ServiceIdRules.Validate(suggestion!, suggestion: null, failures);

            // Assert
            // A suggested service id must itself pass validation.
            Assert.Empty(failures);
        }

        [Theory]
        [InlineData("2048", TestDisplayName = "digits only, no ASCII letter to satisfy the rule")]
        [InlineData("写真共有", TestDisplayName = "non-Latin script yields nothing legal")]
        [InlineData("---", TestDisplayName = "punctuation only")]
        [InlineData("", TestDisplayName = "empty")]
        [InlineData(null, TestDisplayName = "null")]
        public void UnsalvageableApplicationName_SuggestsNothing(string? applicationName)
        {
            // Arrange
            // (no setup — Suggest is pure)

            // Act
            var suggestion = ServiceIdRules.Suggest(applicationName);

            // Assert
            Assert.Null(suggestion);
        }

        [Fact]
        public void DistinctApplicationNames_CanCollideAfterTruncation()
        {
            // The reason a derived value is only ever suggested and never applied as a default:
            // ServiceId decides which installs discover one another, so a silent collision would
            // let unrelated sibling apps rendezvous. Pinned so nobody "improves" Suggest into a
            // default without confronting this.

            // Arrange
            var service = "Contoso Field Service";
            var sales = "Contoso Field Sales";

            // Act
            var suggestedForService = ServiceIdRules.Suggest(service);
            var suggestedForSales = ServiceIdRules.Suggest(sales);

            // Assert
            Assert.Equal(suggestedForService, suggestedForSales);
        }
    }
}
