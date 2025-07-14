// Example consumer workflow for Dyalog.Hmon.Client

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dyalog.Hmon.Client.Lib;
using Serilog;

class Program
{
    static async Task RunMonitoringService(CancellationToken cancellationToken)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Debug()
            .CreateLogger();

        Console.WriteLine("Starting Dyalog.Hmon.Client monitoring service...");

        await using var orchestrator = new HmonOrchestrator();

        // Unified event stream: start as early as possible
        var eventTask = Task.Run(async () =>
        {
            await foreach (var evt in orchestrator.Events.WithCancellation(cancellationToken))
            {
                Log.Information("Event: {EventType} (Session: {SessionId})", evt.GetType().Name, evt.SessionId);
            }
        });

        orchestrator.ClientConnected += (args) =>
        {
            Log.Information("[+] CONNECTED: {Name} (Session: {SessionId})", args.FriendlyName ?? args.Host, args.SessionId);

            // Shared timeout for orchestrator operations
            var orchestratorTimeout = TimeSpan.FromSeconds(20);

            Log.Debug("Subscribing to UntrappedSignal events for session {SessionId}", args.SessionId);
            _ = Task.Run(async () =>
            {
                try
                {
                    var subscribeTask = orchestrator.SubscribeAsync(args.SessionId, [SubscriptionEvent.UntrappedSignal], cancellationToken);
                    if (await Task.WhenAny(subscribeTask, Task.Delay(orchestratorTimeout, cancellationToken)) == subscribeTask)
                    {
                        // Log.Debug("AFTER await SubscribeAsync"); // Removed redundant log
                    }
                    else
                    {
                        Log.Error("SubscribeAsync timed out after {Timeout} seconds", orchestratorTimeout.TotalSeconds);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception during SubscribeAsync");
                }
                Log.Debug("Subscription to UntrappedSignal complete for session {SessionId}", args.SessionId);
            });

            Log.Debug("Starting PollFacts for Workspace and ThreadCount for session {SessionId}", args.SessionId);
            _ = Task.Run(async () =>
            {
                try
                {
                    var pollTask = orchestrator.PollFactsAsync(args.SessionId, [FactType.Workspace, FactType.ThreadCount], TimeSpan.FromSeconds(5), cancellationToken);
                    if (await Task.WhenAny(pollTask, Task.Delay(orchestratorTimeout, cancellationToken)) == pollTask)
                    {
                        Log.Debug("PollFacts started for session {SessionId}", args.SessionId);
                    }
                    else
                    {
                        Log.Error("PollFactsAsync timed out after {Timeout} seconds", orchestratorTimeout.TotalSeconds);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception during PollFactsAsync");
                }
            });

            return Task.CompletedTask;
        };

        orchestrator.ClientDisconnected += (args) =>
        {
            Log.Warning("[-] DISCONNECTED: {Name}. Reason: {Reason}", args.FriendlyName ?? args.Host, args.Reason);
            return Task.CompletedTask;
        };

        // Add the local server for demo
        orchestrator.AddServer("127.0.0.1", 8080, "ExampleServer");

        Log.Information("Press Enter to exit...");
        Console.ReadLine();
        // Cleanup is handled by Main
        await orchestrator.DisposeAsync();
        Log.CloseAndFlush();
    }

    static async Task Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        var runTask = RunMonitoringService(cts.Token);
        Console.ReadLine();
        cts.Cancel();
        await runTask;
    }
}
