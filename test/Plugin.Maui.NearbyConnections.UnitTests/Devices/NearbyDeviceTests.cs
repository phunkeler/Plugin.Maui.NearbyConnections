using System.Threading.Channels;

namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestCategory("Devices")]
public class NearbyDeviceTests
{
    /// <summary>
    /// A connection with no behaviour, for building a <see cref="DeviceState.Connected"/>. Nothing
    /// here sends or receives — the tests below only need an instance to hang off the state.
    /// </summary>
    static NearbyConnection CreateConnection(NearbyDevice device)
        => new(
            device,
            Channel.CreateUnbounded<NearbyPayload>(),
            (_, _) => ValueTask.CompletedTask,
            (_, _, _) => Task.CompletedTask,
            () => ValueTask.CompletedTask);


    [TestClass]
    public sealed class EqualsMethod : NearbyDeviceTests
    {
        [TestMethod]
        public void SameId_ReturnsTrue()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");
            var b = new NearbyDevice("ep1", "Alice");

            // Act
            var result = a.Equals(b);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void SameId_DifferentDisplayName_ReturnsTrue()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");
            var b = new NearbyDevice("ep1", "Bob");

            // Act
            var result = a.Equals(b);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void DifferentId_ReturnsFalse()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");
            var b = new NearbyDevice("ep2", "Alice");

            // Act
            var result = a.Equals(b);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void SameReference_ReturnsTrue()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");

            // Act
            var result = a.Equals(a);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Null_ReturnsFalse()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");

            // Act
            var result = a.Equals(null);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void NonDeviceObject_ReturnsFalse()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");

            // Act
            var result = a.Equals((object)"ep1");

            // Assert
            Assert.IsFalse(result);
        }
    }

    [TestClass]
    public sealed class EqualityOperator : NearbyDeviceTests
    {
        [TestMethod]
        public void SameId_ReturnsTrue()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");
            var b = new NearbyDevice("ep1", "Alice");

            // Act
            var result = a == b;

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void DifferentId_ReturnsFalse()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");
            var b = new NearbyDevice("ep2", "Alice");

            // Act
            var result = a == b;

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void LeftNull_ReturnsFalse()
        {
            // Arrange
            NearbyDevice? a = null;
            var b = new NearbyDevice("ep1", "Alice");

            // Act
            var result = a == b;

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void RightNull_ReturnsFalse()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");
            NearbyDevice? b = null;

            // Act
            var result = a == b;

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void BothNull_ReturnsTrue()
        {
            // Arrange
            NearbyDevice? a = null;
            NearbyDevice? b = null;

            // Act
            var result = a == b;

            // Assert
            Assert.IsTrue(result);
        }
    }

    [TestClass]
    public sealed class InequalityOperator : NearbyDeviceTests
    {
        [TestMethod]
        public void SameId_ReturnsFalse()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");
            var b = new NearbyDevice("ep1", "Alice");

            // Act
            var result = a != b;

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void DifferentId_ReturnsTrue()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");
            var b = new NearbyDevice("ep2", "Alice");

            // Act
            var result = a != b;

            // Assert
            Assert.IsTrue(result);
        }
    }

    [TestClass]
    public sealed class HashCode : NearbyDeviceTests
    {
        [TestMethod]
        public void SameId_ReturnsSameHashCode()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");
            var b = new NearbyDevice("ep1", "Bob");

            // Act
            var result = a.GetHashCode() == b.GetHashCode();

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void DifferentId_ReturnsDifferentHashCode()
        {
            // Arrange
            var a = new NearbyDevice("ep1", "Alice");
            var b = new NearbyDevice("ep2", "Alice");

            // Act
            var result = a.GetHashCode() == b.GetHashCode();

            // Assert
            Assert.IsFalse(result);
        }
    }

    [TestClass]
    public sealed class Identity : NearbyDeviceTests
    {
        // The load-bearing guarantee of the record -> observable class change. PeerRegistry and
        // _activeConnections key on NearbyDevice, so if identity shifted as a device moved through
        // its lifecycle, every existing dictionary entry would be stranded — a device would connect
        // and then be unreachable by lookup.
        [TestMethod]
        public void HashCodeAndEquality_AreStable_AcrossStateTransitions()
        {
            // Arrange
            var device = new NearbyDevice("ep1", "Alice");
            var sameId = new NearbyDevice("ep1", "Alice");
            var originalHash = device.GetHashCode();

            var dictionary = new Dictionary<NearbyDevice, string> { [device] = "tracked" };

            // Act — walk the full lifecycle
            device.State = new DeviceState.RequestReceived();
            device.State = new DeviceState.Connecting(ConnectionRole.Acceptor);
            device.State = new DeviceState.Connected(ConnectionRole.Acceptor, CreateConnection(device));
            device.DisplayName = "Alice (renamed)";

            // Assert — identity never moved, so the entry is still reachable
            Assert.AreEqual(originalHash, device.GetHashCode());
            Assert.IsTrue(device.Equals(sameId));
            Assert.IsTrue(device == sameId);
            Assert.IsTrue(dictionary.TryGetValue(device, out var tracked));
            Assert.AreEqual("tracked", tracked);
            Assert.IsTrue(dictionary.ContainsKey(sameId));
        }

        [TestMethod]
        public void Constructor_NullId_Throws()
            => Assert.ThrowsExactly<ArgumentNullException>(() => new NearbyDevice(null!, "Alice"));

        [TestMethod]
        public void NewDevice_StartsVisible()
        {
            // Arrange
            var device = new NearbyDevice("ep1", "Alice");

            // Assert
            Assert.AreEqual(NearbyDeviceStatus.Visible, device.Status);
            Assert.IsInstanceOfType<DeviceState.Visible>(device.State);
        }

        // Every DeviceState case must project to a status. A new case added without extending the
        // projection throws, and this is what catches that.
        [TestMethod]
        public void Status_ProjectsEveryState()
        {
            // Arrange
            var device = new NearbyDevice("ep1", "Alice");
            var connection = CreateConnection(device);

            var cases = new (DeviceState State, NearbyDeviceStatus Expected)[]
            {
                (new DeviceState.Visible(), NearbyDeviceStatus.Visible),
                (new DeviceState.RequestReceived(), NearbyDeviceStatus.RequestReceived),
                (new DeviceState.Connecting(ConnectionRole.Initiator), NearbyDeviceStatus.Connecting),
                (new DeviceState.Connected(ConnectionRole.Acceptor, connection), NearbyDeviceStatus.Connected),
            };

            // Act & Assert
            foreach (var (state, expected) in cases)
            {
                device.State = state;

                Assert.AreEqual(expected, device.Status, $"{state.GetType().Name} projected wrongly.");
            }
        }
    }

    [TestClass]
    public sealed class PropertyChangedNotification : NearbyDeviceTests
    {
        // The tripwire. Status is derived from State, and consumers bind to Status — so a State
        // write that raises only nameof(State) leaves every bound row frozen, with no compile error
        // and nothing else in this suite failing. Nothing but this test catches that.
        [TestMethod]
        public void State_RaisesPropertyChanged_ForBothStateAndStatus()
        {
            // Arrange
            var device = new NearbyDevice("ep1", "Alice");
            var raised = new List<string?>();
            device.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            // Act
            device.State = new DeviceState.Connecting(ConnectionRole.Initiator);

            // Assert
            Assert.Contains(nameof(NearbyDevice.State), raised);
            Assert.Contains(nameof(NearbyDevice.Status), raised);
        }

        [TestMethod]
        public void DisplayName_RaisesPropertyChanged()
        {
            // Arrange
            var device = new NearbyDevice("ep1", "Alice");
            var raised = new List<string?>();
            device.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            // Act
            device.DisplayName = "Bob";

            // Assert
            Assert.Contains(nameof(NearbyDevice.DisplayName), raised);
        }

        [TestMethod]
        public void State_Null_Throws()
        {
            // Arrange
            var device = new NearbyDevice("ep1", "Alice");

            // Act & Assert
            Assert.ThrowsExactly<ArgumentNullException>(() => device.State = null!);
        }

        // Suppressing no-op sets keeps bindings from re-rendering on every redundant platform
        // callback — the platforms re-report unchanged state routinely. This relies on record
        // equality: a second, distinct Connecting(Initiator) instance is equal to the first, so
        // allocating per transition costs no spurious notifications.
        [TestMethod]
        public void SettingEqualState_DoesNotRaisePropertyChanged()
        {
            // Arrange
            var device = new NearbyDevice("ep1", "Alice")
            {
                State = new DeviceState.Connecting(ConnectionRole.Initiator),
            };

            var raised = new List<string?>();
            device.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            // Act
            device.State = new DeviceState.Connecting(ConnectionRole.Initiator);
            device.DisplayName = "Alice";

            // Assert
            Assert.IsEmpty(raised);
        }
    }
}
