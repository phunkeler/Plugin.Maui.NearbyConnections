namespace Plugin.Maui.NearbyConnections.UnitTests;

[TestClass]
public sealed class NearbyConnectionsTests : IDisposable
{
    readonly INearbyConnections _sut;

    public NearbyConnectionsTests()
    {
        var loggerFactory = new Microsoft.Extensions.Logging.LoggerFactory();
        var options = new NearbyConnectionsOptions();
        _sut = new NearbyConnections(options, loggerFactory, TimeProvider.System);
    }

    public void Dispose() => throw new NotImplementedException();

    [TestMethod]
    public void TestMethod1()
    {
    }
}