using Dyalog.Hmon.OtelAdapter;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Serilog OpenTelemetry sink
using Serilog;

using Spectre.Console;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("config.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var logLevel = config["LogLevel"] ?? "Information";
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(Enum.TryParse<Serilog.Events.LogEventLevel>(logLevel, true, out var lvl) ? lvl : Serilog.Events.LogEventLevel.Information)
    .WriteTo.Console()
    .CreateLogger();

AnsiConsole.MarkupLine("[bold green]HMON-to-OTEL Adapter starting...[/]");

Log.Debug("Loaded configuration: {@Config}", config);

//AnsiConsole.MarkupLine("[bold yellow]Dumping all configuration values...[/]");
//foreach (var (key, value) in config.AsEnumerable().OrderBy(c => c.Key))
//{
//    AnsiConsole.MarkupLine($"[dim]{key}[/] = [yellow]{value}[/]");
//}
var adapterConfig = config.Get<AdapterConfig>();

var validator = new AdapterConfigValidator();
var validationResult = validator.Validate(null, adapterConfig ?? new AdapterConfig());
if (adapterConfig == null || validationResult.Failed) {
  Log.Error("Configuration validation failed: {Errors}", validationResult.Failures);
  AnsiConsole.MarkupLine("[bold red]Configuration validation failed.[/]");
  return;
}

var host = Host.CreateDefaultBuilder(args)
    .UseSerilog()
    .ConfigureServices((context, services) => {
      services.AddSingleton(adapterConfig);
      services.AddHostedService<AdapterService>();
    })
    .Build();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (sender, eventArgs) => {
  Log.Information("Ctrl+C pressed, shutting down...");
  eventArgs.Cancel = true;
  cts.Cancel();
};

await host.RunAsync(cts.Token);
