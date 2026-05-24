namespace Plugin.Maui.NearbyConnections.UiTests.Appium;

internal sealed class NearbyTestFixture : IDisposable
{
    private const string DefaultAppiumServer = "http://localhost:4723";
    private const string DefaultAppPackage = "com.phunkeler.nearbychat";

    public AppiumAgent Advertiser { get; }
    public AppiumAgent Discoverer { get; }

    public NearbyTestFixture(Uri appiumServer, string device1Serial, string device2Serial, string appPackage)
    {
        Advertiser = new AppiumAgent(appiumServer, device1Serial, appPackage, $"advertiser:{device1Serial}");
        Discoverer = new AppiumAgent(appiumServer, device2Serial, appPackage, $"discoverer:{device2Serial}");
    }

    public static NearbyTestFixture FromEnvironment()
    {
        var server = new Uri(GetEnv("APPIUM_SERVER_URL", DefaultAppiumServer));
        var device1 = RequiredEnv("DEVICE1_SERIAL");
        var device2 = RequiredEnv("DEVICE2_SERIAL");
        var appPackage = GetEnv("APP_PACKAGE", DefaultAppPackage);
        return new NearbyTestFixture(server, device1, device2, appPackage);
    }

    public void ResetBothToMainPage()
    {
        Parallel.Invoke(
            () => Advertiser.ReturnToMainPage(),
            () => Discoverer.ReturnToMainPage());
    }

    public void Dispose()
    {
        Advertiser.Dispose();
        Discoverer.Dispose();
    }

    private static string GetEnv(string key, string fallback) =>
        Environment.GetEnvironmentVariable(key) ?? fallback;

    internal static string RequiredEnv(string key) =>
        Environment.GetEnvironmentVariable(key)
            ?? throw new InvalidOperationException(
                $"Required environment variable '{key}' is not set. " +
                "Configure it in NearbyConnections.runsettings or as an environment variable.");
}
