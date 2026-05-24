using System.Collections.ObjectModel;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace Plugin.Maui.NearbyConnections.UiTests.Appium;

internal sealed class AppiumAgent : IDisposable
{
    private readonly AndroidDriver<IWebElement> _driver;
    private bool _disposed;

    public string Label { get; }

    public AppiumAgent(Uri serverUrl, string deviceSerial, string appPackage, string label)
    {
        Label = label;
        var options = new AppiumOptions();
        options.PlatformName = "Android";
        options.AddAdditionalCapability("appium:automationName", "UIAutomator2");
        options.AddAdditionalCapability("appium:udid", deviceSerial);
        options.AddAdditionalCapability("appium:appPackage", appPackage);
        options.AddAdditionalCapability("appium:appActivity", $"{appPackage}.MainActivity");
        options.AddAdditionalCapability("appium:noReset", true);
        options.AddAdditionalCapability("appium:newCommandTimeout", 120);
        _driver = new AndroidDriver<IWebElement>(serverUrl, options, TimeSpan.FromSeconds(120));
    }

    public void Tap(string accessibilityId)
    {
        _driver.FindElement(MobileBy.AccessibilityId(accessibilityId)).Click();
    }

    public void Fill(string accessibilityId, string text)
    {
        var el = _driver.FindElement(MobileBy.AccessibilityId(accessibilityId));
        el.Clear();
        el.SendKeys(text);
    }

    public void WaitForElement(string accessibilityId, TimeSpan timeout)
    {
        NewWait(timeout).Until(d =>
        {
            try { return d.FindElement(MobileBy.AccessibilityId(accessibilityId)) is not null; }
            catch (NoSuchElementException) { return false; }
        });
    }

    public void WaitForText(string accessibilityId, string expectedText, TimeSpan timeout)
    {
        NewWait(timeout).Until(d =>
        {
            try { return d.FindElement(MobileBy.AccessibilityId(accessibilityId)).Text == expectedText; }
            catch (NoSuchElementException) { return false; }
        });
    }

    public void WaitForElementByText(string text, TimeSpan timeout)
    {
        NewWait(timeout).Until(d =>
            d.FindElements(MobileBy.AndroidUIAutomator($"new UiSelector().text(\"{text}\")")).Count > 0);
    }

    public IReadOnlyList<string> WaitForElementsByPrefix(string prefix, TimeSpan timeout)
    {
        ReadOnlyCollection<IWebElement>? found = null;
        NewWait(timeout).Until(d =>
        {
            found = d.FindElements(
                MobileBy.AndroidUIAutomator($"new UiSelector().descriptionStartsWith(\"{prefix}\")"));
            return found.Count > 0;
        });
        return found?
            .Select(e => e.GetAttribute("content-desc") ?? string.Empty)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList() ?? [];
    }

    public void WaitForNoElementsByPrefix(string prefix, TimeSpan timeout)
    {
        NewWait(timeout).Until(d =>
            d.FindElements(
                MobileBy.AndroidUIAutomator($"new UiSelector().descriptionStartsWith(\"{prefix}\")"))
             .Count == 0);
    }

    public void Screenshot(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "evidence");
        File.WriteAllBytes(path, _driver.GetScreenshot().AsByteArray);
    }

    public void ReturnToMainPage()
    {
        for (var i = 0; i < 5; i++)
        {
            try
            {
                _driver.FindElement(MobileBy.AccessibilityId("BackButton")).Click();
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
            catch (NoSuchElementException)
            {
                return;
            }
        }
    }

    private WebDriverWait NewWait(TimeSpan timeout) =>
        new(_driver, timeout) { PollingInterval = TimeSpan.FromMilliseconds(500) };

    public void Dispose()
    {
        if (!_disposed)
        {
            _driver.Quit();
            _disposed = true;
        }
    }
}
