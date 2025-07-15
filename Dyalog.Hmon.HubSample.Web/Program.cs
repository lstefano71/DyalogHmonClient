using Dyalog.Hmon.HubSample.Web;

using Serilog;
using Serilog.Events;

LogEventLevel logLevel = LogEventLevel.Information;
try {
  // Load config first to get log level
  var config = await Dyalog.Hmon.HubSample.Web.ConfigLoader.LoadAsync(args);
  if (Enum.TryParse<Serilog.Events.LogEventLevel>(config.LogLevel, true, out var parsedLevel))
    logLevel = parsedLevel;

  Log.Logger = new LoggerConfiguration()
      .MinimumLevel.Is(logLevel)
      .WriteTo.Console()
      .CreateLogger();

  Log.Information("Starting HMon Hub Sample Web...");
  Log.Information("Loaded configuration: {@Config}", config);

  var aggregator = new FactAggregator();
  var wsHub = new WebSocketHub(aggregator);
  await using var orchestratorService = new HmonHubOrchestratorService(config, aggregator, wsHub);
  await orchestratorService.StartAsync();

  var builder = WebApplication.CreateBuilder(args);
  builder.Host.UseSerilog();

  var app = builder.Build();

  app.UseWebSockets();
  app.Map("/ws", wsHub.HandleWebSocketAsync);

  app.MapGet("/facts", () => new {
    facts = aggregator.GetAllFacts(),
    events = aggregator.GetAllEvents()
  });
  app.MapGet("/status", () => new[] { new { name = "LocalServer", status = "Connected" } }); // Placeholder

  var apiConfig = config.Api;
  app.Urls.Add($"http://{apiConfig.Ip}:{apiConfig.Port}");

  if (config.AutoShutdownSeconds is int seconds && seconds > 0) {
    using var cts = new CancellationTokenSource();
    _ = Task.Run(async () => {
      Log.Information("Auto-shutdown scheduled in {Seconds} seconds.", seconds);
      await Task.Delay(TimeSpan.FromSeconds(seconds), cts.Token);
      Log.Information("Auto-shutdown triggered.");
      await app.StopAsync();
    });
    await app.RunAsync(cts.Token);
  } else {
    await app.RunAsync();
  }
} catch (Exception ex) {
  Log.Fatal(ex, "Application startup failed.");
  Environment.Exit(1);
} finally {
  Log.CloseAndFlush();
}
