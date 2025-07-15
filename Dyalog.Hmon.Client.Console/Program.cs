// Example consumer workflow for Dyalog.Hmon.Client
using Dyalog.Hmon.Client.Lib;

using Spectre.Console;

using System.Collections.Concurrent;

class Program
{
  static async Task RunMonitoringService(CancellationToken cancellationToken)
  {
    AnsiConsole.MarkupLine("[bold green]Starting Dyalog.Hmon.Client monitoring service...[/]");

    await using var orchestrator = new HmonOrchestrator();

    // State tracking
    var servers = new ConcurrentDictionary<Guid, (string Name, string Host)>();
    var facts = new ConcurrentDictionary<Guid, Dictionary<string, string>>();
    var subscriptions = new ConcurrentDictionary<Guid, HashSet<string>>();
    var recentEvents = new ConcurrentDictionary<Guid, List<string>>();

    // Unified event stream: update state on events
    var eventTask = Task.Run(async () => {
      try {
        await foreach (var evt in orchestrator.Events.WithCancellation(cancellationToken)) {
          var sessionId = evt.SessionId;
          // Debug: track all event types for diagnostics
          var debugList = recentEvents.GetOrAdd(sessionId, _ => []);
          debugList.Insert(0, evt.GetType().Name);
          if (debugList.Count > 10) debugList.RemoveAt(debugList.Count - 1);

          switch (evt) {
            case FactsReceivedEvent factsEvt:
              var factDict = facts.GetOrAdd(sessionId, _ => []);
              foreach (var fact in factsEvt.Facts.Facts) {
                string value = fact switch {
                  WorkspaceFact ws => $"Used: {ws.Used}, Available: {ws.Available}, Compactions: {ws.Compactions}, GarbageCollections: {ws.GarbageCollections}, Allocation: {ws.Allocation}",
                  ThreadCountFact tc => $"Total: {tc.Total}, Suspended: {tc.Suspended}",
                  HostFact host => $"Machine: {host.Machine.Name}, PID: {host.Machine.PID}",
                  _ => fact.ToString() ?? ""
                };
                factDict[fact.Name] = value;
              }
              break;
            case SubscribedResponseReceivedEvent subEvt:
              subscriptions[sessionId] = [.. subEvt.Response.Events.Select(e => e.Name)];
              break;
            case NotificationReceivedEvent notifEvt:
              var eventList = recentEvents.GetOrAdd(sessionId, _ => []);
              var eventName = notifEvt.Notification.Event.Name;
              eventList.Insert(0, eventName);
              if (eventList.Count > 5) eventList.RemoveAt(eventList.Count - 1);
              break;
          }
        }
      } catch (OperationCanceledException) {
        // Expected on cancellation, exit gracefully
      }
    }, cancellationToken);

    orchestrator.ClientConnected += async (args) => {
      servers[args.SessionId] = (args.FriendlyName ?? args.Host, args.Host);

      var orchestratorTimeout = TimeSpan.FromSeconds(20);

      // Subscribe to UntrappedSignal
      try {
        var subscribeTask = orchestrator.SubscribeAsync(args.SessionId, [SubscriptionEvent.UntrappedSignal], cancellationToken);
        await subscribeTask;
      } catch { }

      // Poll facts
      try {
        var pollTask = orchestrator.PollFactsAsync(args.SessionId, [FactType.Workspace, FactType.ThreadCount], TimeSpan.FromSeconds(5), cancellationToken);
        await pollTask;
      } catch { }

    };

    orchestrator.ClientDisconnected += (args) => {
      servers.TryRemove(args.SessionId, out _);
      facts.TryRemove(args.SessionId, out _);
      subscriptions.TryRemove(args.SessionId, out _);
      return Task.CompletedTask;
    };

    //orchestrator.AddServer("127.0.0.1", 8080, "Server 1");
    //orchestrator.AddServer("127.0.0.1", 8081, "Server 2");
    var listener = orchestrator.StartListenerAsync("0.0.0.0", 8080, cancellationToken).ContinueWith(t => {
      if (t.IsFaulted) {
        AnsiConsole.MarkupLine("[bold red]Failed to start listener:[/] " + t.Exception?.GetBaseException().Message);
      } else {
        AnsiConsole.MarkupLine("[bold green]Listener started successfully.[/]");
      }
    }, cancellationToken);

    // Live table display
    await AnsiConsole.Live(new Table()
    .AddColumn("SessionId")
    .AddColumn("Name")
    .AddColumn("Host")
    .AddColumn("Facts")
    .AddColumn("Subscribed Events")
    .AddColumn("Recent Events")
).StartAsync(async ctx => {
  try {
    while (!cancellationToken.IsCancellationRequested) {
      var table = new Table()
          .AddColumn("SessionId")
          .AddColumn("Name")
          .AddColumn("Host")
          .AddColumn("Facts")
          .AddColumn("Subscribed Events")
          .AddColumn("Recent Events");

      foreach (var (sessionId, (name, host)) in servers) {
        var factStr = facts.TryGetValue(sessionId, out var fs)
            ? string.Join("\n", fs.Select(kv => $"{kv.Key}: {kv.Value}"))
            : "";
        var subStr = subscriptions.TryGetValue(sessionId, out var subs)
            ? string.Join(", ", subs)
            : (recentEvents.TryGetValue(sessionId, out var recentEvList) && recentEvList.Any(e => e == "UntrappedSignal") ? "UntrappedSignal" : "");
        var eventsStr = recentEvents.TryGetValue(sessionId, out var evList)
            ? string.Join("\n", evList)
            : "";
        table.AddRow(sessionId.ToString(), name, host, factStr, subStr, eventsStr);
      }

      ctx.UpdateTarget(table);
      await Task.Delay(500, cancellationToken);
    }
  } catch (TaskCanceledException) {
    // Graceful exit on cancellation
  }
});

    await eventTask;
    await orchestrator.DisposeAsync();
  }

  static async Task Main(string[] args)
  {
    using var cts = new CancellationTokenSource();
    var runTask = RunMonitoringService(cts.Token);
    Console.ReadLine();
    cts.Cancel();
    try {
      await runTask;
    } catch (OperationCanceledException) {
      // Graceful shutdown
    }
  }
}
