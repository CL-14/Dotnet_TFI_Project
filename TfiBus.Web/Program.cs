using TfiBus.Web.Components;
using TfiBus.Core;
var builder = WebApplication.CreateBuilder(args);

// The path comes from configuration, so the same code works on your laptop and on boxy.
string dbPath = builder.Configuration["Collector:DatabasePath"] ?? "collector.db";
builder.Services.AddSingleton(new CollectorReader(dbPath));

string stopsPath = builder.Configuration["Gtfs:StopsPath"] ?? "GTFS_Realtime/stops.txt";
// Loaded once when the site starts and shared by every page.
builder.Services.AddSingleton(BusLogic.BuildStopLookup(stopsPath));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
