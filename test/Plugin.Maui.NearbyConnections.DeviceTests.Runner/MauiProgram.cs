using DeviceRunners.VisualRunners;

using Microsoft.Extensions.Logging;

namespace Plugin.Maui.NearbyConnections.DeviceTests.Runner;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseVisualTestRunner(conf => conf
                .AddCliConfiguration()
                .AddConsoleResultChannel()
                .AddTestAssembly(typeof(Plugin.Maui.NearbyConnections.DeviceTests.AssemblyMarker).Assembly)
                .AddXunit3());

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
