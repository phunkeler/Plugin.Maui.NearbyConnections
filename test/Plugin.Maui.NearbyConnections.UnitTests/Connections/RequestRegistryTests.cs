using Microsoft.Extensions.Time.Testing;

namespace Plugin.Maui.NearbyConnections.UnitTests;

/// <summary>
/// Covers <see cref="RequestRegistry"/> — the one owner of "an inbound request is outstanding for
/// device X". The atomic claim is the arbiter between accept, reject, and expiry: exactly one wins.
/// </summary>
[Trait("Category", "Connections")]
public class RequestRegistryTests
{
    static readonly TimeSpan Timeout30 = TimeSpan.FromSeconds(30);

    static NearbyOptions Options(TimeSpan timeout) => new() { InboundRequestTimeout = timeout };

    public sealed class Tracking : RequestRegistryTests
    {
        [Fact]
        public void Track_ReturnsTheExpiryDeadline()
        {
            // Arrange
            var time = new FakeTimeProvider();
            var registry = Create.RequestRegistry(time, options: Options(Timeout30));
            var expected = time.GetUtcNow() + Timeout30;

            // Act
            var expiresAt = registry.Track(Create.Request());

            // Assert
            Assert.Equal(expected, expiresAt);
            Assert.True(registry.Contains("peer-1"));
        }

        [Fact]
        public async Task Track_WithInfiniteTimeout_ArmsNoTimer()
        {
            // Arrange
            var expired = 0;
            var time = new FakeTimeProvider();
            var registry = Create.RequestRegistry(
                time,
                onExpired: _ => { expired++; return Task.CompletedTask; },
                options: Options(System.Threading.Timeout.InfiniteTimeSpan));

            // Act
            var expiresAt = registry.Track(Create.Request());
            time.Advance(TimeSpan.FromDays(1));
            await Task.Yield();

            // Assert
            Assert.Null(expiresAt);
            Assert.Equal(0, expired);
            Assert.True(registry.Contains("peer-1"));
        }
    }

    public sealed class Claiming : RequestRegistryTests
    {
        [Fact]
        public void TryClaim_WinsExactlyOnce()
        {
            // Arrange
            var time = new FakeTimeProvider();
            var registry = Create.RequestRegistry(time, options: Options(Timeout30));
            var request = Create.Request();
            registry.Track(request);

            // Act
            var first = registry.TryClaim("peer-1", out var claimed);
            var second = registry.TryClaim("peer-1", out _);

            // Assert
            Assert.True(first);
            Assert.Same(request, claimed);
            Assert.False(second);
            Assert.False(registry.Contains("peer-1"));
        }

        [Fact]
        public async Task ClaimedRequest_NeverExpires()
        {
            // The accept side of the race: once claimed, the timer must be a no-op.

            // Arrange
            var expired = 0;
            var time = new FakeTimeProvider();
            var registry = Create.RequestRegistry(
                time,
                onExpired: _ => { expired++; return Task.CompletedTask; },
                options: Options(Timeout30));
            registry.Track(Create.Request());

            // Act
            registry.TryClaim("peer-1", out _);
            time.Advance(Timeout30 + TimeSpan.FromSeconds(1));
            await Task.Yield();

            // Assert
            Assert.Equal(0, expired);
        }

        [Fact]
        public async Task ExpiredRequest_CannotBeClaimed()
        {
            // The expiry side of the race: once the timer wins, a later accept must lose.

            // Arrange
            var expiredRequests = new List<NearbyConnectionRequest>();
            var time = new FakeTimeProvider();
            var registry = Create.RequestRegistry(
                time,
                onExpired: r => { expiredRequests.Add(r); return Task.CompletedTask; },
                options: Options(Timeout30));
            var request = Create.Request();
            registry.Track(request);

            // Act
            time.Advance(Timeout30 + TimeSpan.FromSeconds(1));
            await Wait.UntilAsync(() => expiredRequests.Count > 0);
            var claimedAfterExpiry = registry.TryClaim("peer-1", out _);

            // Assert
            Assert.Same(request, Assert.Single(expiredRequests));
            Assert.False(claimedAfterExpiry);
            Assert.False(registry.Contains("peer-1"));
        }

        [Fact]
        public async Task NewRequestForTheSameDevice_ReplacesTheOldTimer()
        {
            // The old timer must not stay armed against the replacement request.

            // Arrange
            var expiredRequests = new List<NearbyConnectionRequest>();
            var time = new FakeTimeProvider();
            var registry = Create.RequestRegistry(
                time,
                onExpired: r => { expiredRequests.Add(r); return Task.CompletedTask; },
                options: Options(Timeout30));
            var first = Create.Request();
            var second = Create.Request();

            // Act — the second Track replaces the first, re-arming from the later instant.
            registry.Track(first);
            time.Advance(TimeSpan.FromSeconds(15));
            registry.Track(second);
            time.Advance(Timeout30 + TimeSpan.FromSeconds(1));
            await Wait.UntilAsync(() => expiredRequests.Count > 0);

            // Assert
            Assert.Same(second, Assert.Single(expiredRequests));
        }
    }

    public sealed class Teardown : RequestRegistryTests
    {
        [Fact]
        public async Task ClaimAll_ClaimsEverythingAndCancelsEveryTimer()
        {
            // Arrange
            var expired = 0;
            var time = new FakeTimeProvider();
            var registry = Create.RequestRegistry(
                time,
                onExpired: _ => { expired++; return Task.CompletedTask; },
                options: Options(Timeout30));
            registry.Track(Create.Request(Create.Device("peer-1")));
            registry.Track(Create.Request(Create.Device("peer-2")));

            // Act
            var claimed = registry.ClaimAll();
            time.Advance(Timeout30 + TimeSpan.FromSeconds(1));
            await Task.Yield();

            // Assert
            Assert.Equal(2, claimed.Length);
            Assert.Equal(0, expired);
            Assert.False(registry.Contains("peer-1"));
            Assert.False(registry.Contains("peer-2"));
        }
    }
}
