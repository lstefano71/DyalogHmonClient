// Example consumer workflow for Dyalog.Hmon.Client
using Dyalog.Hmon.Client.Lib;

using Spectre.Console;
using Spectre.Console.Rendering;

using System.Collections.Concurrent;
using System.Globalization;

class SessionFacts
{
  public WorkspaceFact? Workspace { get; set; }
  public ThreadCountFact? ThreadCount { get; set; }
  public HostFact? Host { get; set; }
  public AccountInformationFact? AccountInformation { get; set; }
  public ThreadsFact? Threads { get; set; }
  public SuspendedThreadsFact? SuspendedThreads { get; set; }
  // Add more fact types as needed
}

class Program
{
  // Track carousel step across refreshes
  static int _carouselStep = 0;

  static async Task RunMonitoringService(CancellationToken cancellationToken)
  {
    AnsiConsole.MarkupLine("[bold green]Starting Dyalog.Hmon.Client monitoring service...[/]");

    await using var orchestrator = new HmonOrchestrator();

    // State tracking
    var servers = new ConcurrentDictionary<Guid, (string Name, string Host)>();
    var facts = new ConcurrentDictionary<Guid, SessionFacts>();
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
              var sessionFacts = facts.GetOrAdd(sessionId, _ => new SessionFacts());
              foreach (var fact in factsEvt.Facts.Facts) {
                switch (fact) {
                  case WorkspaceFact ws:
                    sessionFacts.Workspace = ws;
                    break;
                  case ThreadCountFact tc:
                    sessionFacts.ThreadCount = tc;
                    break;
                  case HostFact host:
                    sessionFacts.Host = host;
                    break;
                  case AccountInformationFact acc:
                    sessionFacts.AccountInformation = acc;
                    break;
                  case ThreadsFact threads:
                    sessionFacts.Threads = threads;
                    break;
                  case SuspendedThreadsFact sthreads:
                    sessionFacts.SuspendedThreads = sthreads;
                    break;
                    // Add more fact types as needed
                }
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
        var pollTask = orchestrator.PollFactsAsync(args.SessionId,
          [FactType.Workspace, FactType.ThreadCount, FactType.Host, FactType.AccountInformation,
           FactType.Threads, FactType.SuspendedThreads],
          TimeSpan.FromSeconds(5), cancellationToken);
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
          //          .AddColumn("Host")
          .AddColumn("Facts");
      //          .AddColumn("Subscribed Events")
      //          .AddColumn("Recent Events");

      foreach (var (sessionId, (name, host)) in servers) {
        // Render HostFact under SessionId if present
        IRenderable sessionIdCell;
        if (facts.TryGetValue(sessionId, out var sessionFacts) && sessionFacts.Host is not null) {
          var grid = new Grid();
          grid.AddColumn();
          grid.AddRow(new Text(sessionId.ToString()));
          grid.AddRow(RenderHostFactTable(sessionFacts.Host, host));
          sessionIdCell = grid;
        } else {
          sessionIdCell = new Text(sessionId.ToString());
        }
        // Render only one fact table at a time, carousel style, using local modulo
        IRenderable factCell;
        if (facts.TryGetValue(sessionId, out sessionFacts)) {
          factCell = RenderFactsTableCarousel(sessionFacts, _carouselStep);
        } else {
          factCell = new Text("");
        }

        var subStr = subscriptions.TryGetValue(sessionId, out var subs)
            ? string.Join(", ", subs)
            : (recentEvents.TryGetValue(sessionId, out var recentEvList) && recentEvList.Any(e => e == "UntrappedSignal") ? "UntrappedSignal" : "");
        var eventsStr = recentEvents.TryGetValue(sessionId, out var evList)
            ? string.Join("\n", evList)
            : "";
        table.AddRow(sessionIdCell, new Text(name), factCell);
      }

      ctx.UpdateTarget(table);
      _carouselStep++; // Just increment, let each session use modulo of its own available facts
      await Task.Delay(1000, cancellationToken); // 1 second interval
    }
  } catch (TaskCanceledException) {
    // Graceful exit on cancellation
  }
});

    await eventTask;
    await orchestrator.DisposeAsync();
  }

  // Helper to render facts as nested tables with minimal borders
  static IRenderable RenderFactsTable(SessionFacts facts)
  {
    var grid = new Grid();
    grid.AddColumn();
    grid.AddColumn();
    if (facts.Workspace is not null)
      grid.AddRow(RenderWorkspaceFactTable(facts.Workspace));
    if (facts.ThreadCount is not null)
      grid.AddRow(RenderThreadCountFactTable(facts.ThreadCount));
    if (facts.Threads is not null)
      grid.AddRow(RenderThreadsFactTable(facts.Threads));
    if (facts.SuspendedThreads is not null)
      grid.AddRow(RenderSuspendedThreadsFactTable(facts.SuspendedThreads));
    if (facts.AccountInformation is not null)
      grid.AddRow(RenderAccountInformationFactTable(facts.AccountInformation));
    return grid;
  }

  static Table RenderWorkspaceFactTable(WorkspaceFact ws)
  {
    var table = new Table();
    table.Border(TableBorder.Simple);
    table.AddColumn(new TableColumn("[bold]Workspace[/]").LeftAligned());
    table.AddColumn(new TableColumn("").RightAligned());
    table.AddRow("WSID", ws.WSID);
    table.AddRow("Available", ws.Available.ToString("N0", CultureInfo.InvariantCulture));
    table.AddRow("Used", ws.Used.ToString("N0", CultureInfo.InvariantCulture));
    table.AddRow("Compactions", ws.Compactions.ToString("N0", CultureInfo.InvariantCulture));
    table.AddRow("GarbageCollections", ws.GarbageCollections.ToString("N0", CultureInfo.InvariantCulture));
    table.AddRow("GarbagePockets", ws.GarbagePockets.ToString("N0", CultureInfo.InvariantCulture));
    table.AddRow("FreePockets", ws.FreePockets.ToString("N0", CultureInfo.InvariantCulture));
    table.AddRow("UsedPockets", ws.UsedPockets.ToString("N0", CultureInfo.InvariantCulture));
    table.AddRow("Sediment", ws.Sediment.ToString("N0", CultureInfo.InvariantCulture));
    table.AddRow("Allocation", ws.Allocation.ToString("N0", CultureInfo.InvariantCulture));
    table.AddRow("AllocationHWM", ws.AllocationHWM.ToString("N0", CultureInfo.InvariantCulture));
    table.AddRow("TrapReserveWanted", ws.TrapReserveWanted.ToString("N0", CultureInfo.InvariantCulture));
    table.AddRow("TrapReserveActual", ws.TrapReserveActual.ToString("N0", CultureInfo.InvariantCulture));
    return table;
  }

  static Table RenderThreadCountFactTable(ThreadCountFact tc)
  {
    var table = new Table();
    table.Border(TableBorder.Simple);
    table.AddColumn(new TableColumn("[bold]ThreadCount[/]").LeftAligned());
    table.AddColumn(new TableColumn("").RightAligned());
    table.AddRow("Total", tc.Total.ToString());
    table.AddRow("Suspended", tc.Suspended.ToString());
    return table;
  }

  static Table RenderHostFactTable(HostFact host, string hostName)
  {
    var table = new Table();
    table.Border(TableBorder.Simple);
    table.AddColumn(new TableColumn("[bold]Host[/]").LeftAligned());
    table.AddColumn(new TableColumn(hostName).RightAligned());
    table.AddRow("Machine.Name", host.Machine.Name);
    table.AddRow("Machine.User", host.Machine.User);
    table.AddRow("Machine.PID", host.Machine.PID.ToString());
    table.AddRow("Machine.AccessLevel", host.Machine.AccessLevel.ToString());
    table.AddRow("Interpreter.Version", host.Interpreter.Version);
    table.AddRow("Interpreter.BitWidth", host.Interpreter.BitWidth.ToString());
    table.AddRow("Interpreter.IsUnicode", host.Interpreter.IsUnicode.ToString());
    table.AddRow("Interpreter.IsRuntime", host.Interpreter.IsRuntime.ToString());
    table.AddRow("Interpreter.SessionUUID", host.Interpreter.SessionUUID ?? "");
    //table.AddRow("CommsLayer.Version", host.CommsLayer?.Version ?? "");
    //table.AddRow("CommsLayer.Address", host.CommsLayer?.Address ?? "");
    //table.AddRow("CommsLayer.Port4", host.CommsLayer?.Port4.ToString());
    //table.AddRow("CommsLayer.Port6", host.CommsLayer?.Port6.ToString());
    table.AddRow("RIDE.Listening", host.RIDE.Listening.ToString());
    table.AddRow("RIDE.HTTPServer", host.RIDE.HTTPServer?.ToString() ?? "");
    table.AddRow("RIDE.Version", host.RIDE.Version ?? "");
    table.AddRow("RIDE.Address", host.RIDE.Address ?? "");
    table.AddRow("RIDE.Port4", host.RIDE.Port4?.ToString() ?? "");
    table.AddRow("RIDE.Port6", host.RIDE.Port6?.ToString() ?? "");
    return table;
  }

  static Table RenderAccountInformationFactTable(AccountInformationFact acc)
  {
    var table = new Table();
    table.Border(TableBorder.Simple);
    table.AddColumn(new TableColumn("[bold]AccountInformation[/]").LeftAligned());
    table.AddColumn(new TableColumn("").RightAligned());
    table.AddRow("UserIdentification", acc.UserIdentification.ToString());
    table.AddRow("ComputeTime", acc.ComputeTime.ToString("N0"));
    table.AddRow("ConnectTime", acc.ConnectTime.ToString("N0"));
    table.AddRow("KeyingTime", acc.KeyingTime.ToString("N0"));
    return table;
  }

  static Table RenderThreadsFactTable(ThreadsFact threads)
  {
    var table = new Table();
    table.Border(TableBorder.Simple);
    table.AddColumn(new TableColumn("[bold]Threads[/]").LeftAligned());
    table.AddColumn(new TableColumn("").RightAligned());
    foreach (var t in threads.Values) {
      table.AddRow($"TID {t.Tid}", $"State: {t.State}, Suspended: {t.Suspended}, Flags: {t.Flags}");
      if (t.Stack != null) {
        foreach (var s in t.Stack) {
          table.AddRow("  Stack", $"Restricted: {s.Restricted}, Desc: {Markup.Escape(s.Description)}");
        }
      }
      if (t.DMX != null) {
        table.AddRow("  DMX", $"{t.DMX.EM}/{t.DMX.EN} {Markup.Escape(string.Join(' ', t.DMX.DM))}");
      }
      if (t.Exception != null) {
        table.AddRow("  Exception", $"Msg: {t.Exception.Message}");
      }
    }
    return table;
  }

  static Table RenderSuspendedThreadsFactTable(SuspendedThreadsFact threads)
  {
    var table = new Table();
    table.Border(TableBorder.Simple);
    table.AddColumn(new TableColumn("[bold]SuspendedThreads[/]").LeftAligned());
    table.AddColumn(new TableColumn("").RightAligned());
    foreach (var t in threads.Values) {
      table.AddRow($"TID {t.Tid}", $"State: {t.State}, Suspended: {t.Suspended}, Flags: {t.Flags}");
      if (t.Stack != null) {
        foreach (var s in t.Stack) {
          table.AddRow("  Stack", $"Restricted: {s.Restricted}, Desc: {Markup.Escape(s.Description)}");
        }
      }
      if (t.DMX != null) {
        table.AddRow("  DMX", $"Category: {t.DMX.Category}, EM: {t.DMX.EM}");
      }
      if (t.Exception != null) {
        table.AddRow("  Exception", $"Msg: {Markup.Escape(t.Exception.Message)}");
      }
    }
    return table;
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

  // Helper to render only one fact table at a time, carousel style
  static IRenderable RenderFactsTableCarousel(SessionFacts facts, int step)
  {
    // Order: Workspace, ThreadCount, Threads, SuspendedThreads, AccountInformation
    var tables = new List<IRenderable>();
    if (facts.Workspace is not null)
      tables.Add(RenderWorkspaceFactTable(facts.Workspace));
    if (facts.ThreadCount is not null)
      tables.Add(RenderThreadCountFactTable(facts.ThreadCount));
    if (facts.Threads is not null)
      tables.Add(RenderThreadsFactTable(facts.Threads));
    if (facts.SuspendedThreads is not null)
      tables.Add(RenderSuspendedThreadsFactTable(facts.SuspendedThreads));
    if (facts.AccountInformation is not null)
      tables.Add(RenderAccountInformationFactTable(facts.AccountInformation));
    if (tables.Count == 0)
      return new Text("");
    // Carousel: show one table at a time, using local modulo
    var idx = step % tables.Count;
    return tables[idx];
  }
}
