namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestClass]
public sealed class NearbyConnectionsTests : IDisposable
{
    readonly NearbyConnectionsImplementation _sut;

    public NearbyConnectionsTests()
    {
        _sut = new NearbyConnectionsImplementation();
    }

    public void Dispose()
        => _sut.Dispose();

    [TestMethod]
    public void TestMethod1()
    {
    }
}