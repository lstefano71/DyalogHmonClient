using Serilog;
using Spectre.Console;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Serilog.Extensions.Hosting;

namespace Dyalog.Hmon.OtelAdapter;

class Program
{
    static void Main(string[] args)
    {
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

        var host = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                services.AddHostedService<AdapterService>();
            })
            .Build();

        host.Run();
    }
}
