using Dyalog.Hmon.OtelAdapter;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Serilog OpenTelemetry sink
using Serilog;

using Spectre.Console;

// Modern C# 13 top-level async entry point
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
    .WriteTo.OpenTelemetry(endpoint: config["OtelExporter:Endpoint"])
    .CreateLogger();

AnsiConsole.MarkupLine("[bold green]HMON-to-OTEL Adapter starting...[/]");

Log.Debug("Loaded configuration: {@Config}", config);

var adapterConfig = config.Get<AdapterConfig>();
var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(adapterConfig ?? new AdapterConfig());
var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
if (adapterConfig == null || !System.ComponentModel.DataAnnotations.Validator.TryValidateObject(adapterConfig, validationContext, validationResults, true)) {
  Log.Error("Configuration validation failed: {Errors}", validationResults.Select(r => r.ErrorMessage).ToList());
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
