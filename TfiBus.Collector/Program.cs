using TfiBus.Collector;
using TfiBus.Core;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// The factory (the part after =>) tells the host how to build a FeedClient.
builder.Services.AddSingleton<FeedClient>(services =>
{
    // Environment variables are exposed through configuration, so this reads TFI_API_KEY
    string apiKey = builder.Configuration["TFI_API_KEY"] ?? "";

    // The collector runs for months, so connections are renewed every 5 minutes
    // instead of living forever (this picks up DNS changes).
    SocketsHttpHandler handler = new SocketsHttpHandler
    { PooledConnectionLifetime = TimeSpan.FromMinutes(5),

    // Drops idle connections after 30 s, so a poll (about every 61 s) never reuses a connection that is closing.
    PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30)
    };

    // The trip-updates feed is about 5 MB, so allow 30 seconds before giving up.
    HttpClient http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };

    // Throws a clear error at startup if the key is missing: better than failing on every poll.
    return new FeedClient(http, apiKey);
});

builder.Services.AddSingleton<CollectorDatabase>(services =>
{
    string path = builder.Configuration["Collector:DatabasePath"] ?? "collector.db";
    return new CollectorDatabase(path);
});

builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();
host.Run();
