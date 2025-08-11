using DeviceRunners.VisualRunners;
using Microsoft.Extensions.Logging;

namespace Plugin.Maui.NearbyConnections.DeviceTests;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder.UseVisualTestRunner(conf => conf
                .AddTestAssembly(typeof(MauiProgram).Assembly)
                .AddXunit());

        /*
        builder.UseXHarnessTestRunner(conf => conf
                .AddTestAssembly(typeof(MauiProgram).Assembly)
                .AddXunit());
        */

#if DEBUG
        builder.Logging.AddDebug();
#else
		builder.Logging.AddConsole();
#endif

        return builder.Build();
    }
}
