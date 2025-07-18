using Dyalog.Hmon.HubSample.Web;

using Microsoft.Extensions.Options;

using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Explicitly add config.json to configuration sources
builder.Configuration.AddJsonFile("config.json", optional: true, reloadOnChange: true);

// Bind config from config.json/environment/CLI
builder.Services.AddOptions<HubSampleConfig>()
    .BindConfiguration(string.Empty)
    .ValidateDataAnnotations()
    .Services.AddSingleton<IValidateOptions<HubSampleConfig>, HubSampleConfigValidator>();

// Use Serilog with log level from config if available
LogEventLevel logLevel = LogEventLevel.Information;
var logLevelConfig = builder.Configuration["LogLevel"];
if (!string.IsNullOrWhiteSpace(logLevelConfig) &&
    Enum.TryParse<Serilog.Events.LogEventLevel>(logLevelConfig, true, out var parsedLevel))
  logLevel = parsedLevel;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(logLevel)
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

var app = builder.Build();

// Validate config and get strongly-typed options
var config = app.Services.GetRequiredService<IOptions<HubSampleConfig>>().Value;
Log.Information("Starting HMon Hub Sample Web...");
Log.Information("Loaded configuration: {@Config}", config);

var aggregator = new FactAggregator();
var wsHub = new WebSocketHub(aggregator);
await using var orchestratorService = new HmonHubOrchestratorService(config, aggregator, wsHub);
await orchestratorService.StartAsync();

app.UseWebSockets();
app.Map("/ws", wsHub.HandleWebSocketAsync);

app.MapGet("/facts", () => new {
  facts = aggregator.GetAllFacts(),
  events = aggregator.GetAllEvents()
});
app.MapGet("/status", () => new[] { new { name = "LocalServer", status = "Connected" } }); // Placeholder

var apiConfig = config.Api;
app.Urls.Add($"http://{apiConfig.Ip}:{apiConfig.Port}");

// Register orchestrator disposal on shutdown with timeout
app.Lifetime.ApplicationStopping.Register(() => {
  Log.Information("Disposing orchestrator on shutdown...");
  var disposeTask = orchestratorService.DisposeAsync().AsTask();
  if (!disposeTask.Wait(TimeSpan.FromSeconds(10))) {
    Log.Warning("Orchestrator disposal timed out. Forcing shutdown.");
  }
});

if (config.AutoShutdownSeconds is int seconds && seconds > 0) {
  _ = Task.Run(async () => {
    Log.Information("Auto-shutdown scheduled in {Seconds} seconds.", seconds);
    await Task.Delay(TimeSpan.FromSeconds(seconds));
    Log.Information("Auto-shutdown triggered.");
    await app.StopAsync();
  });
}
await app.RunAsync();

Log.CloseAndFlush();
