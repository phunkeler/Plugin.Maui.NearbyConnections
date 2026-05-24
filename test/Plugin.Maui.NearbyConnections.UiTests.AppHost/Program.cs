var builder = DistributedApplication.CreateBuilder(args);

// Appium is started fresh per CI run and torn down when this process exits.
// The container reaches the Pi host's ADB server via the host-gateway alias
// (equivalent to extra_hosts: ["host-gateway:host-gateway"] in docker compose).
builder.AddContainer("appium", "ghcr.io/phunkeler/nearbyconnections-appium", "latest")
    .WithHttpEndpoint(port: 4723, name: "http")
    .WithContainerRuntimeArgs("--add-host", "host-gateway:host-gateway")
    .WithEnvironment("ANDROID_ADB_SERVER_HOST", "host-gateway")
    .WithEnvironment("ANDROID_ADB_SERVER_PORT", "5037");

builder.Build().Run();
