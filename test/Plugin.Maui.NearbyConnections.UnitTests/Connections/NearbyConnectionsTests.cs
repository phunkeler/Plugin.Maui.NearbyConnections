using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Maui.Dispatching;
using NSubstitute;

namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestCategory("Connections")]
public class NearbyConnectionsTests : IDisposable
{
    readonly FakeTimeProvider _timeProvider;
    readonly NearbyDeviceManager _deviceManager;
    readonly NearbyConnectionsImplementation _sut;

    public NearbyConnectionsTests()
    {
        _timeProvider = new FakeTimeProvider();
        _deviceManager = new NearbyDeviceManager(_timeProvider, (_, _, _) => { });

        _sut = new NearbyConnectionsImplementation(
            _deviceManager,
            Substitute.For<IDispatcher>(),
            _timeProvider,
            new NearbyConnectionsOptions { MarshalEventsToMainThread = false },
            NullLogger.Instance);
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

    [TestClass]
    public sealed class ConnectionRequested : NearbyConnectionsTests
    {
        [TestMethod]
        public void InboundRequest_AddsDeviceToDevices()
        {
            // Arrange
            var device = _deviceManager.GetOrAddDevice("ep1", "Peer", NearbyDeviceState.ConnectionRequestedInbound);

            // Act
            _sut.OnConnectionRequested(device, _timeProvider.GetUtcNow());

            // Assert
            CollectionAssert.Contains(_sut.Devices, device);
        }

        [TestMethod]
        public void InboundRequest_WhenDeviceAlreadyPresent_DoesNotDuplicate()
        {
            // Arrange
            var device = _deviceManager.GetOrAddDevice("ep1", "Peer", NearbyDeviceState.ConnectionRequestedInbound);
            _sut.OnConnectionRequested(device, _timeProvider.GetUtcNow());

            // Act
            _sut.OnConnectionRequested(device, _timeProvider.GetUtcNow());

            // Assert
            Assert.HasCount(1, _sut.Devices);
        }
    }

    [TestClass]
    public sealed class ConnectionResponded : NearbyConnectionsTests
    {
        /// <summary>
        /// Inbound (advertiser-side) rejection: the device was only present because of the
        /// connection request, so it must be removed from Devices on rejection.
        /// </summary>
        [TestMethod]
        public void InboundRejected_RemovesDeviceFromDevices()
        {
            // Arrange
            var device = _deviceManager.GetOrAddDevice("ep1", "Peer", NearbyDeviceState.ConnectionRequestedInbound);
            _sut.OnConnectionRequested(device, _timeProvider.GetUtcNow());

            // Inbound rejection: platform removes the device from the manager before raising the event
            _deviceManager.RemoveDevice("ep1");

            // Act
            _sut.OnConnectionResponded(device, _timeProvider.GetUtcNow(), accepted: false);

            // Assert
            CollectionAssert.DoesNotContain(_sut.Devices, device);
        }

        [TestMethod]
        public void InboundRejected_RaisesConnectionRespondedEvent()
        {
            // Arrange
            var device = _deviceManager.GetOrAddDevice("ep1", "Peer", NearbyDeviceState.ConnectionRequestedInbound);
            _sut.OnConnectionRequested(device, _timeProvider.GetUtcNow());
            _deviceManager.RemoveDevice("ep1");

            NearbyDeviceRespondedEventArgs? raised = null;
            _sut.ConnectionResponded += (_, e) => raised = e;

            // Act
            _sut.OnConnectionResponded(device, _timeProvider.GetUtcNow(), accepted: false);

            // Assert
            Assert.IsNotNull(raised);
            Assert.IsFalse(raised.Accepted);
            Assert.AreSame(device, raised.NearbyDevice);
        }

        [TestMethod]
        public void InboundAccepted_DeviceRemainsInDevices()
        {
            // Arrange
            var device = _deviceManager.GetOrAddDevice("ep1", "Peer", NearbyDeviceState.ConnectionRequestedInbound);
            _sut.OnConnectionRequested(device, _timeProvider.GetUtcNow());
            _deviceManager.SetState("ep1", NearbyDeviceState.Connected);

            // Act
            _sut.OnConnectionResponded(device, _timeProvider.GetUtcNow(), accepted: true);

            // Assert
            CollectionAssert.Contains(_sut.Devices, device);
        }

        /// <summary>
        /// Outbound (discoverer-side) rejection: the device was independently discovered and
        /// is still advertising, so it must remain in Devices at Discovered state.
        /// </summary>
        [TestMethod]
        public void OutboundRejected_DeviceRemainsInDevicesAsDiscovered()
        {
            // Arrange
            var device = _deviceManager.RecordDeviceFound("ep1", "Peer");
            _sut.OnDeviceFound(device, _timeProvider.GetUtcNow());

            // Outbound rejection: platform keeps the device in the manager — it is still advertising

            // Act
            _sut.OnConnectionResponded(device, _timeProvider.GetUtcNow(), accepted: false);

            // Assert
            CollectionAssert.Contains(_sut.Devices, device);
            Assert.AreEqual(NearbyDeviceState.Discovered, device.State);
        }

        [TestMethod]
        public void OutboundAccepted_DeviceRemainsInDevices()
        {
            // Arrange
            var device = _deviceManager.RecordDeviceFound("ep1", "Peer");
            _sut.OnDeviceFound(device, _timeProvider.GetUtcNow());
            _deviceManager.SetState("ep1", NearbyDeviceState.Connected);

            // Act
            _sut.OnConnectionResponded(device, _timeProvider.GetUtcNow(), accepted: true);

            // Assert
            CollectionAssert.Contains(_sut.Devices, device);
        }
    }

    [TestClass]
    public sealed class DeviceFound : NearbyConnectionsTests
    {
        [TestMethod]
        public void Found_AddsDeviceToDevices()
        {
            // Arrange
            var device = _deviceManager.RecordDeviceFound("ep1", "Peer");

            // Act
            _sut.OnDeviceFound(device, _timeProvider.GetUtcNow());

            // Assert
            CollectionAssert.Contains(_sut.Devices, device);
        }

        [TestMethod]
        public void Found_WhenDeviceAlreadyPresent_DoesNotDuplicate()
        {
            // Arrange
            var device = _deviceManager.RecordDeviceFound("ep1", "Peer");
            _sut.OnDeviceFound(device, _timeProvider.GetUtcNow());

            // Act
            _sut.OnDeviceFound(device, _timeProvider.GetUtcNow());

            // Assert
            Assert.HasCount(1, _sut.Devices);
        }
    }

    [TestClass]
    public sealed class DeviceLost : NearbyConnectionsTests
    {
        [TestMethod]
        public void Lost_RemovesDeviceFromDevices()
        {
            // Arrange
            var device = _deviceManager.RecordDeviceFound("ep1", "Peer");
            _sut.OnDeviceFound(device, _timeProvider.GetUtcNow());

            // Act
            _sut.OnDeviceLost(device, _timeProvider.GetUtcNow());

            // Assert
            CollectionAssert.DoesNotContain(_sut.Devices, device);
        }
    }

    [TestClass]
    public sealed class SendBytes : NearbyConnectionsTests
    {
        [TestMethod]
        public void NullDevice_ThrowsArgumentNullException()
        {
            // Arrange
            var data = new byte[] { 1, 2, 3 };

            // Act & Assert
            Assert.ThrowsExactly<ArgumentNullException>(() => _sut.SendAsync(null!, data));
        }

        [TestMethod]
        public void NullData_ThrowsArgumentNullException()
        {
            // Arrange
            var device = _deviceManager.GetOrAddDevice("ep1", "Peer", NearbyDeviceState.Connected);
            byte[] nullData = null!;

            // Act & Assert
            Assert.ThrowsExactly<ArgumentNullException>(() => _sut.SendAsync(device, nullData));
        }

        [TestMethod]
        public void DeviceNotConnected_ThrowsInvalidOperationException()
        {
            // Arrange
            var device = _deviceManager.RecordDeviceFound("ep1", "Peer");

            // Act & Assert
            Assert.ThrowsExactly<InvalidOperationException>(() => _sut.SendAsync(device, new byte[] { 1 }));
        }

        [TestMethod]
        public void EmptyData_ReturnsCompletedTask()
        {
            // Arrange
            var device = _deviceManager.GetOrAddDevice("ep1", "Peer", NearbyDeviceState.Connected);

            // Act
            var task = _sut.SendAsync(device, []);

            // Assert
            Assert.IsTrue(task.IsCompletedSuccessfully);
        }
    }

    [TestClass]
    public sealed class DeviceDisconnected : NearbyConnectionsTests
    {
        [TestMethod]
        public void Disconnected_RemovesDeviceFromDevices()
        {
            // Arrange
            var device = _deviceManager.RecordDeviceFound("ep1", "Peer");
            _sut.OnDeviceFound(device, _timeProvider.GetUtcNow());
            _deviceManager.SetState("ep1", NearbyDeviceState.Connected);

            // Act
            _sut.OnDeviceDisconnected(device, _timeProvider.GetUtcNow());

            // Assert
            CollectionAssert.DoesNotContain(_sut.Devices, device);
        }
    }
}
