using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using WorkPlanner.Client;
using WorkPlanner.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Load configuration from appsettings.json
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

builder.Logging.SetMinimumLevel(LogLevel.Information);

// HttpClient with cookies enabled - allow relative /api in Docker/Dev
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:7191";
var baseAddress = apiBaseUrl.StartsWith("/")
    ? new Uri(builder.HostEnvironment.BaseAddress)
    : new Uri(apiBaseUrl);

builder.Services.AddScoped<BrowserCredentialsHandler>();
builder.Services.AddScoped(sp => new HttpClient(sp.GetRequiredService<BrowserCredentialsHandler>())
{
    BaseAddress = baseAddress,
    DefaultRequestHeaders = { { "Accept", "application/json" } }
});

// MudBlazor
builder.Services.AddMudServices();

// Custom services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<WorkEntryService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<SprintService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<TaskCommentService>();

var app = builder.Build();

// Initialize auth state
var authService = app.Services.GetRequiredService<AuthService>();
await authService.RefreshUserAsync();

await app.RunAsync();
