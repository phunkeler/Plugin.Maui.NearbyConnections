#if IOS
using Xunit;
using Plugin.Maui.NearbyConnections;

namespace Plugin.Maui.NearbyConnections.DeviceTests.Tests;

public class GetPeerIdTests
{
    [Fact]
    public void TestGetPeerId()
    {
        var _sut = new NearbyConnectionsManager();
        var peerId = _sut.GetPeerId("Test");

        Assert.NotNull(peerId);
    }

    [Fact]
    public void GetPeerId_SameDisplayName_ReturnsStablePeerIdAcrossRestarts()
    {
        const string displayName = "StableTestPeer";
        
        // First "app run" - create and get peer ID
        MCPeerID? firstPeerId;
        using (var firstManager = new NearbyConnectionsManager())
        {
            firstPeerId = firstManager.GetPeerId(displayName);
            Assert.NotNull(firstPeerId);
        }
        
        // Second "app run" - simulate app restart by creating new manager instance
        MCPeerID? secondPeerId;
        using (var secondManager = new NearbyConnectionsManager())
        {
            secondPeerId = secondManager.GetPeerId(displayName);
            Assert.NotNull(secondPeerId);
        }
        
        // Third "app run" - verify stability across multiple restarts
        MCPeerID? thirdPeerId;
        using (var thirdManager = new NearbyConnectionsManager())
        {
            thirdPeerId = thirdManager.GetPeerId(displayName);
            Assert.NotNull(thirdPeerId);
        }
        
        // Verify all peer IDs are the same
        Assert.Equal(firstPeerId.DisplayName, secondPeerId.DisplayName);
        Assert.Equal(firstPeerId.DisplayName, thirdPeerId.DisplayName);
        
        // Archive and compare the peer IDs to ensure they're truly the same instance
        var archiver = new NSKeyedPeerIdArchiver();
        var firstData = archiver.ArchivePeerId(firstPeerId);
        var secondData = archiver.ArchivePeerId(secondPeerId);
        var thirdData = archiver.ArchivePeerId(thirdPeerId);
        
        Assert.True(firstData.IsEqualTo(secondData), "First and second peer IDs should have identical archived data");
        Assert.True(firstData.IsEqualTo(thirdData), "First and third peer IDs should have identical archived data");
        Assert.True(secondData.IsEqualTo(thirdData), "Second and third peer IDs should have identical archived data");
    }

}


#endif