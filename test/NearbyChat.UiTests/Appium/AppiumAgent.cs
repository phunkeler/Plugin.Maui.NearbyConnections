using System.Collections.ObjectModel;

namespace NearbyChat.UiTests.Appium;

internal sealed class AppiumAgent : IDisposable
{
    private readonly AndroidDriver<IWebElement> _driver;
    private readonly string _appPackage;
    private bool _disposed;

    public string Label { get; }
    public string DeviceSerial { get; }

    public AppiumAgent(Uri serverUrl, string deviceSerial, string appPackage, string appActivity, string label,
        string? adbHost = null, int adbPort = 5037, int? systemPort = null, int? mjpegServerPort = null)
    {
        Label = label;
        DeviceSerial = deviceSerial;
        _appPackage = appPackage;

        var options = new AppiumOptions
        {
            PlatformName = "Android"
        };
        options.AddAdditionalCapability("appium:automationName", "UIAutomator2");
        options.AddAdditionalCapability("appium:udid", deviceSerial);
        options.AddAdditionalCapability("appium:appPackage", appPackage);
        // .NET for Android mangles the launcher Activity's Java class name (e.g.
        // "crc6424d577a2eb62e007.MainActivity") — it is not derivable from the
        // ApplicationId/appPackage, and can change between builds. UiAutomator2
        // normally auto-detects it by reading the APK's manifest, but that
        // detection is skipped entirely when appium:noReset is set (Appium
        // assumes the app is already installed and has no manifest to read) —
        // so appActivity MUST be supplied explicitly here. The caller resolves
        // it at CI time via `adb shell cmd package resolve-activity`.
        options.AddAdditionalCapability("appium:appActivity", appActivity);
        options.AddAdditionalCapability("appium:noReset", true);
        // MainActivity requests runtime permissions on every cold start (see
        // MainActivity.OnCreate), which throws up a system dialog that blocks
        // the app's own UI. appium:autoGrantPermissions does NOT help here:
        // the APK is installed out-of-band via raw `adb install` (no `app`
        // capability is ever given to Appium), and UIAutomator2 only grants
        // permissions as part of its own install path, which is skipped
        // entirely when no `app` capability is present. Permissions must
        // instead be granted via `adb shell pm grant` before this driver is
        // constructed — see the "Grant runtime permissions before first
        // launch" step in android-lab's nearbychat-ui-tests.yml.
        options.AddAdditionalCapability("appium:newCommandTimeout", 120);
        // Devices in the lab have screen lock disabled. Skip the driver's unlock
        // check entirely — it's slower and can be flaky on physical devices.
        options.AddAdditionalCapability("appium:skipUnlock", true);
        if (adbHost is not null)
        {
            options.AddAdditionalCapability("appium:adbHost", adbHost);
            options.AddAdditionalCapability("appium:adbPort", adbPort);
        }
        // The lab runs one adb server shared by all Appium containers/devices.
        // UiAutomator2's default system/MJPEG ports collide across concurrent
        // sessions on a shared adb server — each session's "adb forward" for
        // 8200/7810 races the others, leaving some devices' on-device servers
        // (bound fine, e.g. port 6790) unreachable at the expected local port.
        // Assigning unique ports per device avoids the collision.
        if (systemPort is not null)
        {
            options.AddAdditionalCapability("appium:systemPort", systemPort.Value);
        }
        if (mjpegServerPort is not null)
        {
            options.AddAdditionalCapability("appium:mjpegServerPort", mjpegServerPort.Value);
        }

        _driver = new AndroidDriver<IWebElement>(serverUrl, options, TimeSpan.FromSeconds(120));
    }

    // MAUI (as of Controls 10.0.41) does not map AutomationId to Android's
    // ContentDescription when a non-empty AutomationId is set — instead it
    // sets AccessibilityNodeInfo.ViewIdResourceName ("<package>:id/<id>") and
    // clears ContentDescription if it equalled AutomationId (see
    // dotnet/maui's SemanticExtensions.cs). UIAutomator2's "accessibility id"
    // strategy (MobileBy.AccessibilityId) searches content-desc only, so it
    // can never find these elements. MobileBy.Id targets ViewIdResourceName
    // instead, which is what MAUI actually populates — use that everywhere
    // an AutomationId-based lookup is needed.
    private string ResourceId(string automationId) => $"{_appPackage}:id/{automationId}";

    public void Tap(string accessibilityId)
        => _driver.FindElement(MobileBy.Id(ResourceId(accessibilityId))).Click();

    public void TapByText(string text)
        => _driver
            .FindElement(MobileBy.AndroidUIAutomator($"new UiSelector().text(\"{text}\")"))
            .Click();

    public void Fill(string accessibilityId, string text)
    {
        var el = _driver.FindElement(MobileBy.Id(ResourceId(accessibilityId)));
        el.Clear();
        el.SendKeys(text);
    }

    public void WaitForElement(string accessibilityId, TimeSpan timeout)
        => NewWait(timeout).Until(d =>
        {
            try { return d.FindElement(MobileBy.Id(ResourceId(accessibilityId))) is not null; }
            catch (NoSuchElementException) { return false; }
        });

    public void WaitForText(string accessibilityId, string expectedText, TimeSpan timeout)
        => NewWait(timeout).Until(d =>
        {
            try { return d.FindElement(MobileBy.Id(ResourceId(accessibilityId))).Text == expectedText; }
            catch (NoSuchElementException) { return false; }
        });

    public void WaitForElementByText(string text, TimeSpan timeout)
        => NewWait(timeout).Until(d =>
            d.FindElements(MobileBy.AndroidUIAutomator($"new UiSelector().text(\"{text}\")")).Count > 0);

    public void WaitForElementContainingText(string containsText, TimeSpan timeout)
        => NewWait(timeout).Until(d =>
            d.FindElements(
                MobileBy.AndroidUIAutomator($"new UiSelector().textContains(\"{containsText}\")")).Count > 0);

    // resourceIdMatches uses whole-string regex matching (unlike
    // descriptionStartsWith/textStartsWith, there is no *StartsWith
    // equivalent for resource-id) — anchor with a trailing ".*" to emulate
    // a prefix match against the package-qualified resource-id.
    private string ResourceIdPrefixSelector(string prefix) =>
        $"new UiSelector().resourceIdMatches(\"{System.Text.RegularExpressions.Regex.Escape(ResourceId(prefix))}.*\")";

    public IReadOnlyList<string> WaitForElementsByPrefix(string prefix, TimeSpan timeout)
    {
        ReadOnlyCollection<IWebElement> found = [];
        NewWait(timeout).Until(d =>
        {
            found = d.FindElements(MobileBy.AndroidUIAutomator(ResourceIdPrefixSelector(prefix)));
            return found.Count > 0;
        });
        var resourceIdPrefix = ResourceId(string.Empty);
        return found
            .Select(e => e.GetAttribute("resource-id") ?? string.Empty)
            .Where(id => id.StartsWith(resourceIdPrefix, StringComparison.Ordinal))
            .Select(id => id[resourceIdPrefix.Length..])
            .ToList();
    }

    public void WaitForNoElementsByPrefix(string prefix, TimeSpan timeout)
        => NewWait(timeout).Until(d =>
            d.FindElements(MobileBy.AndroidUIAutomator(ResourceIdPrefixSelector(prefix))).Count == 0);

    /// <summary>
    /// Scrolls the first scrollable container until an element with the given text
    /// is visible, then taps it. Use when the target may be below the fold (e.g.
    /// the system photo/video picker grid).
    /// </summary>
    public void ScrollIntoViewByText(string text)
    {
        var selector =
            $"new UiScrollable(new UiSelector().scrollable(true).instance(0))" +
            $".scrollIntoView(new UiSelector().text(\"{text}\"))";
        _driver.FindElement(MobileBy.AndroidUIAutomator(selector)).Click();
    }

    /// <summary>
    /// Pushes a file to the device via Appium's pushFile command.
    /// Goes through the Appium server's existing ADB connection — does not
    /// require adb on the runner's PATH.
    /// </summary>
    public void PushFile(string localPath, string deviceDestination)
    {
        var bytes = File.ReadAllBytes(localPath);
        _driver.PushFile(deviceDestination, bytes);
        // Notify MediaStore so the photo/video picker sees the file immediately.
        AdbShell($"am broadcast -a android.intent.action.MEDIA_SCANNER_SCAN_FILE " +
                 $"-d file://{deviceDestination}");
    }

    public string AdbShell(string command)
    {
        var result = _driver.ExecuteScript("mobile: shell", new Dictionary<string, object>
        {
            ["command"] = command,
        });
        return result?.ToString() ?? string.Empty;
    }

    public void ReturnToMainPage()
    {
        for (var i = 0; i < 5; i++)
        {
            try
            {
                _driver.FindElement(MobileBy.Id(ResourceId("BackButton"))).Click();
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
            catch (NoSuchElementException)
            {
                return;
            }
        }
    }

    public void Screenshot(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? "evidence";
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, _driver.GetScreenshot().AsByteArray);
    }

    public void DumpPageSource(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? "evidence";
        Directory.CreateDirectory(dir);
        File.WriteAllText(path, _driver.PageSource);
    }

    private WebDriverWait NewWait(TimeSpan timeout) =>
        new(_driver, timeout) { PollingInterval = TimeSpan.FromMilliseconds(500) };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _driver.Quit();
        _disposed = true;
    }
}
