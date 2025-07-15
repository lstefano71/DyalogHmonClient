// Example streamlined consumer workflow for Dyalog.Hmon.Client
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
}

class Program
{
  static int _carouselStep = 0;

  static async Task RunMonitoringService(CancellationToken cancellationToken)
  {
    AnsiConsole.MarkupLine("[bold green]Starting Dyalog.Hmon.Client monitoring service...[/]");

    await using var orchestrator = new HmonOrchestrator();

    var servers = new ConcurrentDictionary<Guid, (string Name, string Host)>();
    var facts = new ConcurrentDictionary<Guid, SessionFacts>();
    var recentEvents = new ConcurrentDictionary<Guid, List<string>>();
    var builders = new ConcurrentDictionary<Guid, SessionMonitorBuilder>();

    orchestrator.ClientConnected += async (args) => {
      servers[args.SessionId] = (args.FriendlyName ?? args.Host, args.Host);

      var builder = new SessionMonitorBuilder(orchestrator, args.SessionId)
          .SubscribeTo(SubscriptionEvent.UntrappedSignal)
          .PollFacts(TimeSpan.FromSeconds(5),
              FactType.Workspace, FactType.ThreadCount, FactType.Host,
              FactType.AccountInformation, FactType.Threads, FactType.SuspendedThreads)
          .OnFactChanged(async fact => {
            var sessionFacts = facts.GetOrAdd(args.SessionId, _ => new SessionFacts());
            switch (fact) {
              case WorkspaceFact ws: sessionFacts.Workspace = ws; break;
              case ThreadCountFact tc: sessionFacts.ThreadCount = tc; break;
              case HostFact host: sessionFacts.Host = host; break;
              case AccountInformationFact acc: sessionFacts.AccountInformation = acc; break;
              case ThreadsFact threads: sessionFacts.Threads = threads; break;
              case SuspendedThreadsFact sthreads: sessionFacts.SuspendedThreads = sthreads; break;
            }
            await Task.CompletedTask;
          })
          .OnEvent(async evt => {
            var eventList = recentEvents.GetOrAdd(args.SessionId, _ => []);
            eventList.Insert(0, evt.GetType().Name);
            if (eventList.Count > 10) eventList.RemoveAt(eventList.Count - 1);
            await Task.CompletedTask;
          })
          .WithCancellation(cancellationToken);

      builders[args.SessionId] = builder;
      await builder.StartAsync();
    };

    orchestrator.ClientDisconnected += (args) => {
      servers.TryRemove(args.SessionId, out _);
      facts.TryRemove(args.SessionId, out _);
      recentEvents.TryRemove(args.SessionId, out _);
      if (builders.TryRemove(args.SessionId, out var builder)) {
        // No explicit stop needed; WithCancellation handles it
      }
      return Task.CompletedTask;
    };

    var listener = orchestrator.StartListenerAsync("0.0.0.0", 4501, cancellationToken).ContinueWith(t => {
      if (t.IsFaulted) {
        AnsiConsole.MarkupLine("[bold red]Failed to start listener:[/] " + t.Exception?.GetBaseException().Message);
      } else {
        AnsiConsole.MarkupLine("[bold green]Listener started successfully.[/]");
      }
    }, cancellationToken);

    await AnsiConsole.Live(new Table()
        .AddColumn("SessionId")
        .AddColumn("Name")
        .AddColumn("Facts")
    ).StartAsync(async ctx => {
      try {
        while (!cancellationToken.IsCancellationRequested) {
          var table = new Table()
                  .AddColumn("SessionId")
                  .AddColumn("Name")
                  .AddColumn("Facts");

          foreach (var (sessionId, (name, host)) in servers) {
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

            IRenderable factCell;
            if (facts.TryGetValue(sessionId, out sessionFacts)) {
              factCell = RenderFactsTableCarousel(sessionFacts, _carouselStep);
            } else {
              factCell = new Text("");
            }

            table.AddRow(sessionIdCell, new Text(name), factCell);
          }

          ctx.UpdateTarget(table);
          _carouselStep++;
          await Task.Delay(5000, cancellationToken);
        }
      } catch (TaskCanceledException) {
        // Graceful exit on cancellation
      }
    });

    await orchestrator.DisposeAsync();
  }

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

  static IRenderable RenderFactsTableCarousel(SessionFacts facts, int step)
  {
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
    var idx = step % tables.Count;
    return tables[idx];
  }
}
