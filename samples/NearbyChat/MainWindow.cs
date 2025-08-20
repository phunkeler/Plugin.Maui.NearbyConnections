using NearbyChat.Resources.Styles;
using NearbyChat.Resources.Themes;

namespace NearbyChat;

public class MainWindow : Window
{
    public MainWindow() : base()
    {
    }

    public MainWindow(Page page) : base(page)
    {
    }

    protected override void OnCreated()
    {
        base.OnCreated();

        if (Application.Current is null)
            return;

        // Default to the system theme
        Application.Current.UserAppTheme = AppTheme.Unspecified;

        Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;

        // Detect sysytem theme and apply the corresponding theme
        if (Application.Current.RequestedTheme is AppTheme.Dark ||
            Application.Current.RequestedTheme is AppTheme.Unspecified)
        {
            // If the system theme is dark, apply the dark theme
            var mergedDictionaries = Application.Current.Resources.MergedDictionaries;
            mergedDictionaries.Clear();
            mergedDictionaries.Add(new DarkTheme());
            mergedDictionaries.Add(new MainStyle());

        }
    }

    protected override void OnDestroying()
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;
        }

        base.OnDestroying();
    }

    void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        if (Application.Current?.RequestedTheme is null)
            return;

        var requested = e.RequestedTheme;


        var mergedDictionaries = Application.Current?.Resources.MergedDictionaries;

        if (mergedDictionaries is not null)
        {
            mergedDictionaries.Clear();
            mergedDictionaries.Add(requested is AppTheme.Dark or AppTheme.Unspecified
                ? new DarkTheme()
                : new LightTheme());
            mergedDictionaries.Add(new MainStyle());
        }

    }
}