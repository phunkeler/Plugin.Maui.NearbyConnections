namespace NearbyChat.UiTests.Appium;

internal sealed class NearbyTestFixture : IDisposable
{
    private const string DefaultAppPackage = "com.phunkeler.nearbychat";

    private static readonly int s_deviceCount =
        int.Parse(Environment.GetEnvironmentVariable("DEVICE_COUNT") ?? "3",
            System.Globalization.CultureInfo.InvariantCulture);

    public AppiumAgent Advertiser => All[0];
    public IReadOnlyList<AppiumAgent> Discoverers { get; }
    public IReadOnlyList<AppiumAgent> All { get; }

    private NearbyTestFixture(IReadOnlyList<AppiumAgent> agents)
    {
        All = agents;
        Discoverers = agents.Skip(1).ToList();
    }

    // Base ports for UiAutomator2's system/MJPEG servers. All devices in the
    // lab share one adb server, so concurrent sessions must use distinct
    // ports per device or their "adb forward" calls collide.
    private const int BaseSystemPort = 8200;
    private const int BaseMjpegServerPort = 7810;

    public static NearbyTestFixture FromEnvironment()
    {
        var appPackage = GetEnv("APP_PACKAGE", DefaultAppPackage);
        var adbHost = Environment.GetEnvironmentVariable("ANDROID_ADB_SERVER_HOST");
        var adbPort = int.TryParse(Environment.GetEnvironmentVariable("ANDROID_ADB_SERVER_PORT"),
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var p) ? p : 5037;
        var agents = new List<AppiumAgent>(s_deviceCount);

        for (var i = 1; i <= s_deviceCount; i++)
        {
            var serverUrl = new Uri(RequiredEnv($"APPIUM_{i}_URL"));
            var serial = RequiredEnv($"DEVICE{i}_SERIAL");
            var role = i == 1 ? "advertiser" : $"discoverer{i - 1}";
            agents.Add(new AppiumAgent(serverUrl, serial, appPackage, $"{role}:{serial}", adbHost, adbPort,
                BaseSystemPort + i, BaseMjpegServerPort + i));
        }

        return new NearbyTestFixture(agents);
    }

    public void ResetAllToMainPage()
        => Parallel.ForEach(All, a => a.ReturnToMainPage());

    public void Dispose()
    {
        foreach (var agent in All)
        {
            agent.Dispose();
        }
    }

    internal static string GetEnv(string key, string fallback) =>
        Environment.GetEnvironmentVariable(key) ?? fallback;

    internal static string RequiredEnv(string key) =>
        Environment.GetEnvironmentVariable(key)
            ?? throw new InvalidOperationException(
                $"Required environment variable '{key}' is not set.");
}
