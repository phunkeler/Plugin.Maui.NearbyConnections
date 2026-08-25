namespace Plugin.Maui.NearbyConnections.UnitTests;

[Trait("Category", "Devices")]
public class NearbyDeviceTests
{
    public sealed class EqualsMethod : NearbyDeviceTests
    {
        [Fact]
        public void SameId_ReturnsTrue()
        {
            // Arrange
            var left = new NearbyDevice("ep1", "Alice");
            var right = new NearbyDevice("ep1", "Alice");

            // Act
            var areEqual = left.Equals(right);

            // Assert
            Assert.True(areEqual);
        }

        [Fact]
        public void SameId_DifferentDisplayName_ReturnsTrue()
        {
            // Arrange
            var left = new NearbyDevice("ep1", "Alice");
            var right = new NearbyDevice("ep1", "Bob");

            // Act
            var areEqual = left.Equals(right);

            // Assert
            Assert.True(areEqual);
        }

        [Fact]
        public void DifferentId_ReturnsFalse()
        {
            // Arrange
            var left = new NearbyDevice("ep1", "Alice");
            var right = new NearbyDevice("ep2", "Alice");

            // Act
            var areEqual = left.Equals(right);

            // Assert
            Assert.False(areEqual);
        }

        [Fact]
        public void SameReference_ReturnsTrue()
        {
            // Arrange
            var left = new NearbyDevice("ep1", "Alice");

            // Act
            var areEqual = left.Equals(left);

            // Assert
            Assert.True(areEqual);
        }

        [Fact]
        public void Null_ReturnsFalse()
        {
            // Arrange
            var left = new NearbyDevice("ep1", "Alice");

            // Act
            var areEqual = left.Equals(null);

            // Assert
            Assert.False(areEqual);
        }

        [Fact]
        public void NonDeviceObject_ReturnsFalse()
        {
            // Arrange
            var left = new NearbyDevice("ep1", "Alice");

            // Act
            var areEqual = left.Equals((object)"ep1");

            // Assert
            Assert.False(areEqual);
        }
    }

    public sealed class EqualityOperator : NearbyDeviceTests
    {
        [Fact]
        public void SameId_ReturnsTrue()
        {
            // Arrange
            var left = new NearbyDevice("ep1", "Alice");
            var right = new NearbyDevice("ep1", "Alice");

            // Act
            var areEqual = left == right;

            // Assert
            Assert.True(areEqual);
        }

        [Fact]
        public void DifferentId_ReturnsFalse()
        {
            // Arrange
            var left = new NearbyDevice("ep1", "Alice");
            var right = new NearbyDevice("ep2", "Alice");

            // Act
            var areEqual = left == right;

            // Assert
            Assert.False(areEqual);
        }

    }

    public sealed class HashCode : NearbyDeviceTests
    {
        [Fact]
        public void SameId_ReturnsSameHashCode()
        {
            // Arrange
            var left = new NearbyDevice("ep1", "Alice");
            var right = new NearbyDevice("ep1", "Bob");

            // Act
            var leftHash = left.GetHashCode();
            var rightHash = right.GetHashCode();

            // Assert
            Assert.Equal(leftHash, rightHash);
        }

        [Fact]
        public void DifferentId_ReturnsDifferentHashCode()
        {
            // Arrange
            var left = new NearbyDevice("ep1", "Alice");
            var right = new NearbyDevice("ep2", "Alice");

            // Act
            var leftHash = left.GetHashCode();
            var rightHash = right.GetHashCode();

            // Assert
            Assert.NotEqual(leftHash, rightHash);
        }
    }

    public sealed class Identity : NearbyDeviceTests
    {
        // The load-bearing guarantee. Id-only equality replaces the record's generated member-wise
        // equality, so a device that merely changed status stays the same device: registries and
        // the platform's connection table key on id, and an identity that shifted mid-lifecycle would strand
        // every existing entry. Generated equality would break every assertion below.
        [Fact]
        public void HashCodeAndEquality_AreStable_AcrossStateTransitions()
        {
            // Arrange
            var device = new NearbyDevice("ep1", "Alice");
            var sameId = new NearbyDevice("ep1", "Alice");
            var originalHash = device.GetHashCode();

            var dictionary = new Dictionary<NearbyDevice, string> { [device] = "tracked" };

            // Act — walk the full lifecycle, which for a value means producing new snapshots
            var connected = device
                with { Status = NearbyDeviceStatus.RequestReceived }
                with { Status = NearbyDeviceStatus.Connecting, Role = ConnectionRole.Acceptor }
                with { Status = NearbyDeviceStatus.Connected }
                with { DisplayName = "Alice (renamed)" };

            // Assert — identity never moved, so the entry is still reachable
            Assert.Equal(originalHash, connected.GetHashCode());
            Assert.True(connected.Equals(sameId));
            Assert.True(connected == sameId);
            Assert.True(dictionary.TryGetValue(connected, out var tracked));
            Assert.Equal("tracked", tracked);
            Assert.True(dictionary.ContainsKey(sameId));
        }

        [Fact]
        public void Constructor_NullId_Throws()
            => Assert.Throws<ArgumentNullException>(() => new NearbyDevice(null!, "Alice"));

        [Fact]
        public void NewDevice_StartsVisible()
        {
            // Arrange
            var device = new NearbyDevice("ep1", "Alice");

            // Assert
            Assert.Equal(NearbyDeviceStatus.Visible, device.Status);
            // A device that has only been discovered plays no role.
            Assert.Null(device.Role);
        }
    }

    public sealed class Snapshots : NearbyDeviceTests
    {
        // A device is a value handed out by the session; nothing may write to it afterwards. If any
        // of these properties ever regained a public setter, a consumer could mutate a snapshot the
        // session still holds, and the thread-safety the value type exists to provide would be gone.
        [Fact]
        public void MutableProperties_AreInitOnly()
        {
            // Arrange
            var properties = new[]
            {
                nameof(NearbyDevice.Id),
                nameof(NearbyDevice.DisplayName),
                nameof(NearbyDevice.Status),
                nameof(NearbyDevice.Role),
            };

            // Act
            var settable = properties
                .Select(name => typeof(NearbyDevice).GetProperty(name)!)
                .Where(p => p.SetMethod is { ReturnParameter: var ret }
                    && !ret.GetRequiredCustomModifiers().Any(m => m.Name == "IsExternalInit"))
                .Select(p => p.Name)
                .ToArray();

            // Assert
            // NearbyDevice is a snapshot: every property must be init-only.
            Assert.Empty(settable);
        }

        [Fact]
        public void ToString_ReportsNameIdAndStatus()
        {
            // Arrange
            var device = new NearbyDevice("ep1", "Alice") { Status = NearbyDeviceStatus.Connected };

            // Act
            var text = device.ToString();

            // Assert
            Assert.Equal("Alice [ep1] Connected", text);
        }

        [Fact]
        public void ToString_UnnamedDevice_SaysSo()
        {
            // Arrange
            var device = new NearbyDevice("ep1", displayName: null);

            // Act
            var text = device.ToString();

            // Assert
            Assert.Equal("(unnamed) [ep1] Visible", text);
        }
    }
}
