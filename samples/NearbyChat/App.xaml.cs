namespace NearbyChat;

public partial class App : Application
{
    readonly AppShell _appShell;

    public App(AppShell appShell, NearbyChat.Services.NearbyIngestionService ingestion)
    {
        InitializeComponent();
        _appShell = appShell;

        // Constructing the ingestion service is what starts it; the app only has to keep it
        // alive. The parameter exists to make DI build it at startup.
        _ = ingestion;
    }

    protected override Window CreateWindow(IActivationState? activationState)
        => new(_appShell);
}