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
            var failures = new List<string>();

            ServiceIdRules.Validate(serviceId, failures);

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
            var failures = new List<string>();

            ServiceIdRules.Validate(serviceId, failures);

            Assert.IsNotEmpty(failures, $"'{serviceId}' violates RFC 6335 but was accepted — this would crash on iOS.");
        }

        [TestMethod]
        public void UnsetSentinel_ReportsOnlyTheUnsetMessage()
        {
            // The sentinel violates several rules at once. Reporting all of them would bury the one
            // thing the developer needs to know: the value was never set.
            var failures = new List<string>();

            ServiceIdRules.Validate(ServiceIdRules.Unset, failures);

            Assert.HasCount(1, failures);
            Assert.Contains("has not been set", failures[0], StringComparison.Ordinal);
        }

        [TestMethod]
        public void MultipleViolations_AreAllReported()
        {
            // A developer fixing a value should learn every problem at once rather than one per
            // rebuild. "-A-" is too-short-safe but breaks casing, the letter rule's sibling, and
            // both hyphen rules.
            var failures = new List<string>();

            ServiceIdRules.Validate("-A-", failures);

            Assert.IsGreaterThan(1, failures.Count,
                "Validation stopped at the first violation instead of reporting all of them.");
        }

        [TestMethod]
        public void EmptyServiceId_DefersToTheSharedNullCheck()
        {
            // The shared validator already reports null/empty. Adding four more rule violations
            // here would bury it.
            var failures = new List<string>();

            ServiceIdRules.Validate(string.Empty, failures);

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
            var failures = new List<string>();

            ServiceIdRules.Validate("Nearby_Chat", failures);

            var message = string.Join(" ", failures);
            Assert.Contains("N", message, StringComparison.Ordinal);
            Assert.Contains("_", message, StringComparison.Ordinal);
            Assert.Contains("C", message, StringComparison.Ordinal);
        }

        [TestMethod]
        public void IncludesTheOffendingValue()
        {
            var failures = new List<string>();

            ServiceIdRules.Validate("way-too-long-service-id", failures);

            Assert.Contains("way-too-long-service-id", string.Join(" ", failures), StringComparison.Ordinal);
        }
    }
}
