// Example consumer workflow for Dyalog.Hmon.Client

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dyalog.Hmon.Client.Lib;
using Serilog;

class Program
{
    static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Debug()
            .CreateLogger();

        Console.WriteLine("Starting Dyalog.Hmon.Client example...");

        await using var orchestrator = new HmonOrchestrator();

        // Subscribe to connection events
        orchestrator.ClientConnected += async (e) =>
        {
            Console.WriteLine($"Connected: {e.SessionId} {e.Host}:{e.Port}");
            // Request facts on connect
            var facts = await orchestrator.GetFactsAsync(e.SessionId, Enum.GetValues<FactType>(), CancellationToken.None);
            Console.WriteLine($"Facts received: {facts.Facts.Count()}");
        };
        orchestrator.ClientDisconnected += async (e) =>
        {
            Console.WriteLine($"Disconnected: {e.SessionId} {e.Host}:{e.Port} Reason: {e.Reason}");
            await Task.CompletedTask;
        };

        // Add a server (replace with actual host/port)
        var sessionId = orchestrator.AddServer("127.0.0.1", 8080, "ExampleServer");

        // Subscribe to unified event stream
        var cts = new CancellationTokenSource();
        var eventTask = Task.Run(async () =>
        {
            await foreach (var evt in orchestrator.Events.WithCancellation(cts.Token))
            {
                Console.WriteLine($"Event: {evt.GetType().Name} (Session: {evt.SessionId})");
            }
        });

        Console.WriteLine("Press Enter to exit...");
        Console.ReadLine();
        cts.Cancel();

        await orchestrator.DisposeAsync();
        Log.CloseAndFlush();
    }
}
