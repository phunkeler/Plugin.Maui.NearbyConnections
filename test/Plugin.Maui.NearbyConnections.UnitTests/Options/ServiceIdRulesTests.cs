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
[TestCategory("Options")]
public class ServiceIdRulesTests
{
    [TestClass]
    public sealed class Accepts : ServiceIdRulesTests
    {
        [TestMethod]
        [DataRow("abc-txtchat", DisplayName = "Apple's own documented example")]
        [DataRow("nearbychat", DisplayName = "letters only")]
        [DataRow("a", DisplayName = "single letter, the shortest legal value")]
        [DataRow("chat2", DisplayName = "trailing digit")]
        [DataRow("a1-b2-c3", DisplayName = "multiple separated hyphens")]
        [DataRow("abcdefghijklmno", DisplayName = "exactly 15 characters, the maximum")]
        public void ValidServiceId_ProducesNoFailures(string serviceId)
        {
            // Arrange
            var failures = new List<string>();

            // Act
            ServiceIdRules.Validate(serviceId, suggestion: null, failures);

            // Assert
            Assert.IsEmpty(failures, $"'{serviceId}' is legal per RFC 6335 but was rejected.");
        }
    }

    [TestClass]
    public sealed class Rejects : ServiceIdRulesTests
    {
        [TestMethod]
        [DataRow("abcdefghijklmnop", DisplayName = "16 characters, one over the limit")]
        [DataRow("NearbyChat", DisplayName = "uppercase letters")]
        [DataRow("nearby_chat", DisplayName = "underscore")]
        [DataRow("nearby.chat", DisplayName = "dot")]
        [DataRow("nearby chat", DisplayName = "space")]
        [DataRow("_nearbychat._tcp", DisplayName = "Bonjour service-type form, the likeliest mistake")]
        [DataRow("123", DisplayName = "digits only, no ASCII letter")]
        [DataRow("-chat", DisplayName = "leading hyphen")]
        [DataRow("chat-", DisplayName = "trailing hyphen")]
        [DataRow("near--chat", DisplayName = "adjacent hyphens")]
        public void InvalidServiceId_ProducesAtLeastOneFailure(string serviceId)
        {
            // Arrange
            var failures = new List<string>();

            // Act
            ServiceIdRules.Validate(serviceId, suggestion: null, failures);

            // Assert
            Assert.IsNotEmpty(failures, $"'{serviceId}' violates RFC 6335 but was accepted — this would crash on iOS.");
        }

        [TestMethod]
        public void UnsetSentinel_ReportsOnlyTheUnsetMessage()
        {
            // The sentinel violates several rules at once. Reporting all of them would bury the one
            // thing the developer needs to know: the value was never set.

            // Arrange
            var failures = new List<string>();

            // Act
            ServiceIdRules.Validate(ServiceIdRules.Unset, suggestion: null, failures);

            // Assert
            Assert.HasCount(1, failures);
            Assert.Contains("has not been set", failures[0], StringComparison.Ordinal);
        }

        [TestMethod]
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
            Assert.IsGreaterThan(1, failures.Count,
                "Validation stopped at the first violation instead of reporting all of them.");
        }

        [TestMethod]
        public void EmptyServiceId_DefersToTheSharedNullCheck()
        {
            // The shared validator already reports null/empty. Adding four more rule violations
            // here would bury it.

            // Arrange
            var failures = new List<string>();

            // Act
            ServiceIdRules.Validate(string.Empty, suggestion: null, failures);

            // Assert
            Assert.IsEmpty(failures);
        }
    }

    [TestClass]
    public sealed class FailureMessages : ServiceIdRulesTests
    {
        [TestMethod]
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

        [TestMethod]
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

    [TestClass]
    public sealed class Suggests : ServiceIdRulesTests
    {
        [TestMethod]
        [DataRow("NearbyChat", "nearbychat", DisplayName = "lowercased")]
        [DataRow("My App", "my-app", DisplayName = "space becomes a hyphen")]
        [DataRow("ACME_Delivery", "acme-delivery", DisplayName = "underscore becomes a hyphen")]
        [DataRow("O'Brien & Sons", "o-brien-sons", DisplayName = "a run of punctuation collapses to one hyphen")]
        [DataRow("Contoso Field Service", "contoso-field-s", DisplayName = "truncated to the 15-character limit")]
        [DataRow("Cafe  Munster", "cafe-munster", DisplayName = "repeated separators do not produce adjacent hyphens")]
        public void ApplicationName_DerivesTheExpectedIdentifier(string applicationName, string expected)
        {
            // Arrange
            // (no setup — Suggest is pure)

            // Act
            var suggestion = ServiceIdRules.Suggest(applicationName);

            // Assert
            Assert.AreEqual(expected, suggestion);
        }

        [TestMethod]
        public void DerivedIdentifier_PassesTheRulesItIsSuggestedFor()
        {
            // Arrange
            var failures = new List<string>();
            var suggestion = ServiceIdRules.Suggest("Contoso Field Service");

            // Act
            ServiceIdRules.Validate(suggestion!, suggestion: null, failures);

            // Assert
            Assert.IsEmpty(failures, $"'{suggestion}' was suggested but is itself invalid.");
        }

        [TestMethod]
        [DataRow("2048", DisplayName = "digits only, no ASCII letter to satisfy the rule")]
        [DataRow("写真共有", DisplayName = "non-Latin script yields nothing legal")]
        [DataRow("---", DisplayName = "punctuation only")]
        [DataRow("", DisplayName = "empty")]
        [DataRow(null, DisplayName = "null")]
        public void UnsalvageableApplicationName_SuggestsNothing(string? applicationName)
        {
            // Arrange
            // (no setup — Suggest is pure)

            // Act
            var suggestion = ServiceIdRules.Suggest(applicationName);

            // Assert
            Assert.IsNull(suggestion);
        }

        [TestMethod]
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
            Assert.AreEqual(suggestedForService, suggestedForSales);
        }
    }
}
