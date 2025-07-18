using Dyalog.Hmon.Client.Lib;
using Dyalog.Hmon.OtelAdapter.Logging;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Serilog;

using Spectre.Console;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
namespace Dyalog.Hmon.OtelAdapter;
using System.Collections.Generic;
/// <summary>
/// Background service that connects to HMON servers, polls for facts and events,
/// and translates them into OpenTelemetry metrics and logs for observability.
/// </summary>
public class AdapterService : BackgroundService, IAsyncDisposable
{
  private readonly AdapterConfig _adapterConfig;
  private HmonOrchestrator? _orchestrator;
  private TelemetryFactory? _telemetryFactory;
  private readonly Meter _meter;
  private readonly ConcurrentDictionary<string, SessionMetrics> _sessionMetrics = new();
  private readonly Microsoft.Extensions.Logging.ILogger _otelLogger;
  private readonly Microsoft.Extensions.Logging.ILoggerFactory _otelLoggerFactory;

  // OTEL counters for cumulative metrics
  private readonly Counter<long>? compactionsCounter;
  private readonly Counter<long>? gcCollectionsCounter;
  private readonly Counter<long>? cpuTimeCounter;
  private readonly Counter<long>? connectTimeCounter;
  private readonly Counter<long>? keyingTimeCounter;

  /// <summary>
  /// Holds per-session metric values and tags for OTEL export.
  /// </summary>
  private class SessionMetrics
  {
    /// <summary>Available workspace memory.</summary>
    public long WorkspaceMemoryAvailable;
    /// <summary>Used workspace memory.</summary>
    public long WorkspaceMemoryUsed;
    /// <summary>Total workspace memory allocation.</summary>
    public long WorkspaceMemoryAllocation;
    /// <summary>Workspace memory allocation high-water mark.</summary>
    public long WorkspaceMemoryAllocationHwm;
    /// <summary>Number of workspace compactions.</summary>
    public long WorkspaceCompactions;
    /// <summary>Number of workspace garbage collections.</summary>
    public long WorkspaceGcCollections;
    /// <summary>Number of garbage pockets in workspace.</summary>
    public long WorkspacePocketsGarbage;
    /// <summary>Number of free pockets in workspace.</summary>
    public long WorkspacePocketsFree;
    /// <summary>Number of used pockets in workspace.</summary>
    public long WorkspacePocketsUsed;
    /// <summary>Workspace sediment value.</summary>
    public long WorkspaceSediment;
    /// <summary>Requested workspace trap reserve.</summary>
    public long WorkspaceTrapReserveWanted;
    /// <summary>Actual workspace trap reserve.</summary>
    public long WorkspaceTrapReserveActual;
    /// <summary>Account CPU time.</summary>
    public long AccountCpuTime;
    /// <summary>Account connect time.</summary>
    public long AccountConnectTime;
    /// <summary>Account keying time.</summary>
    public long AccountKeyingTime;
    /// <summary>Total thread count.</summary>
    public long ThreadsTotal;
    /// <summary>Suspended thread count.</summary>
    public long ThreadsSuspended;
    /// <summary>Tags for OTEL metrics and logs.</summary>
    public KeyValuePair<string, object?>[]? Tags;
  }
  /// <summary>
  /// Initializes a new instance of the <see cref="AdapterService"/> class with the specified adapter configuration.
  /// Sets up OpenTelemetry metrics, logging, and prepares session metric tracking.
  /// </summary>
  /// <param name="adapterConfig">Adapter configuration settings.</param>
  public AdapterService(AdapterConfig adapterConfig)
  {
    _adapterConfig = adapterConfig;
    _meter = new Meter(_adapterConfig.MeterName);
    Serilog.Sinks.OpenTelemetry.OtlpProtocol protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc;
    if (!string.IsNullOrWhiteSpace(_adapterConfig.OtelExporter.Protocol)) {
      System.Enum.TryParse(_adapterConfig.OtelExporter.Protocol, true, out protocol);
    }
    _otelLoggerFactory = LoggerFactory.Create(builder => {
      builder.AddSerilog(new LoggerConfiguration()
        .WriteTo.OpenTelemetry(
        endpoint: _adapterConfig.OtelExporter?.Endpoint,
        protocol: protocol)
        .CreateLogger());
    });
    _otelLogger = _otelLoggerFactory.CreateLogger("HmonToOtel");
    // ObservableGauge for each metric, emits per-session measurements with tags
    _meter.CreateObservableGauge("dyalog.workspace.memory.available", () =>
      _sessionMetrics.Values.Select(sm => new Measurement<long>(sm.WorkspaceMemoryAvailable, sm.Tags ?? [])), "Workspace Memory Available");
    _meter.CreateObservableGauge("dyalog.workspace.memory.used", () =>
      _sessionMetrics.Values.Select(sm => new Measurement<long>(sm.WorkspaceMemoryUsed, sm.Tags ?? [])), "Workspace Memory Used");
    _meter.CreateObservableGauge("dyalog.workspace.memory.allocation", () =>
      _sessionMetrics.Values.Select(sm => new Measurement<long>(sm.WorkspaceMemoryAllocation, sm.Tags ?? [])), "Workspace Memory Allocation");
    _meter.CreateObservableGauge("dyalog.workspace.memory.allocation_hwm", () =>
      _sessionMetrics.Values.Select(sm => new Measurement<long>(sm.WorkspaceMemoryAllocationHwm, sm.Tags ?? [])), "Workspace Memory Allocation HWM");

    // Counters for cumulative metrics
    compactionsCounter = _meter.CreateCounter<long>("dyalog.workspace.compactions", "Workspace Compactions");
    gcCollectionsCounter = _meter.CreateCounter<long>("dyalog.workspace.gc.collections", "Workspace GC Collections");
    cpuTimeCounter = _meter.CreateCounter<long>("dyalog.account.cpu_time", "CPU Time");
    connectTimeCounter = _meter.CreateCounter<long>("dyalog.account.connect_time", "Connect Time");
    keyingTimeCounter = _meter.CreateCounter<long>("dyalog.account.keying_time", "Keying Time");

    _meter.CreateObservableGauge("dyalog.workspace.pockets.garbage", () =>
      _sessionMetrics.Values.Select(sm => new Measurement<long>(sm.WorkspacePocketsGarbage, sm.Tags ?? [])), "Workspace Pockets Garbage");
    _meter.CreateObservableGauge("dyalog.workspace.pockets.free", () =>
      _sessionMetrics.Values.Select(sm => new Measurement<long>(sm.WorkspacePocketsFree, sm.Tags ?? [])), "Workspace Pockets Free");
    _meter.CreateObservableGauge("dyalog.workspace.pockets.used", () =>
      _sessionMetrics.Values.Select(sm => new Measurement<long>(sm.WorkspacePocketsUsed, sm.Tags ?? [])), "Workspace Pockets Used");
    _meter.CreateObservableGauge("dyalog.workspace.sediment", () =>
      _sessionMetrics.Values.Select(sm => new Measurement<long>(sm.WorkspaceSediment, sm.Tags ?? [])), "Workspace Sediment");
    _meter.CreateObservableGauge("dyalog.workspace.trapreservewanted", () =>
      _sessionMetrics.Values.Select(sm => new Measurement<long>(sm.WorkspaceTrapReserveWanted, sm.Tags ?? [])), "Workspace Trap Reserve Wanted");
    _meter.CreateObservableGauge("dyalog.workspace.trapreserveactual", () =>
      _sessionMetrics.Values.Select(sm => new Measurement<long>(sm.WorkspaceTrapReserveActual, sm.Tags ?? [])), "Workspace Trap Reserve Actual");
    _meter.CreateObservableGauge("dyalog.threads.total", () =>
      _sessionMetrics.Values.Select(sm => new Measurement<long>(sm.ThreadsTotal, sm.Tags ?? [])), "Total Thread Count");
    _meter.CreateObservableGauge("dyalog.threads.suspended", () =>
      _sessionMetrics.Values.Select(sm => new Measurement<long>(sm.ThreadsSuspended, sm.Tags ?? [])), "Suspended Thread Count");
  }
  /// <summary>
  /// Main execution loop for the background service.
  /// Connects to HMON servers, subscribes to events, and processes incoming data.
  /// </summary>
  /// <param name="stoppingToken">Cancellation token for graceful shutdown.</param>
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    Log.Information("AdapterService started.");
    AnsiConsole.MarkupLine("[bold blue]AdapterService running.[/]");
    Log.Debug("Loaded configuration: {@AdapterConfig}", _adapterConfig);
    Log.Information("OTLP Endpoint: {Endpoint}", _adapterConfig.OtelExporter?.Endpoint ?? "(null)");
    _orchestrator = new HmonOrchestrator();
    _telemetryFactory = new TelemetryFactory(_adapterConfig);

    // Activate polling listener if configured
    if (_adapterConfig.PollListener is not null) {
      try {
        _ = _orchestrator.StartListenerAsync(_adapterConfig.PollListener.Ip, _adapterConfig.PollListener.Port, stoppingToken);
        Log.Information("Started polling listener on {Host}:{Port}", _adapterConfig.PollListener.Ip, _adapterConfig.PollListener.Port);
      } catch (Exception ex) {
        Log.Error(ex, "Failed to start polling listener on {Host}:{Port}", _adapterConfig.PollListener.Ip, _adapterConfig.PollListener.Port);
      }
    }

    // Add SERVE-mode servers
    foreach (var server in _adapterConfig.HmonServers) {
      _orchestrator.AddServer(server.Host, server.Port, server.Name);
      Log.Information("Added HMON server for connection: {Host}:{Port} ({Name})", server.Host, server.Port, server.Name ?? "");
    }

    // Main unified event processing loop
    try {
      await foreach (var hmonEvent in _orchestrator.Events.WithCancellation(stoppingToken)) {
        try {
          switch (hmonEvent) {
            case SessionConnectedEvent connected:
              _otelLogger.LogInformation("Session started {SessionId} from {Host}:{Port}", connected.SessionId, connected.Host, connected.Port);
              var pollingInterval = TimeSpan.FromMilliseconds(_adapterConfig.PollingIntervalMs);
              FactType[] factsToPoll = [FactType.Host, FactType.AccountInformation, FactType.Workspace, FactType.Threads, FactType.SuspendedThreads, FactType.ThreadCount];
              await _orchestrator.PollFactsAsync(connected.SessionId, factsToPoll, pollingInterval, null, stoppingToken);
              Log.Information("Started polling facts for session {SessionId}", connected.SessionId);

              await _orchestrator.SubscribeAsync(connected.SessionId, _adapterConfig.EventsToSubcribeTo, null, stoppingToken);
              Log.Information("Subscribed to events {events} for session {SessionId}",
                _adapterConfig.EventsToSubcribeTo, connected.SessionId);
              break;

            case SessionDisconnectedEvent disconnected:
              _otelLogger.LogError("HMON client disconnected {SessionId}. Reason: {Reason}", disconnected.SessionId, disconnected.Reason);
              _sessionMetrics.TryRemove(disconnected.SessionId.ToString(), out _);
              break;

            case FactsReceivedEvent factsEvent:
              HandleFactsReceivedEvent(factsEvent);
              break;

            case NotificationReceivedEvent notificationEvent:
              await HandleNotificationReceivedEventAsync(notificationEvent, stoppingToken);
              break;

            case UserMessageReceivedEvent userMsgEvent:
              HandleUserMessageReceivedEvent(userMsgEvent);
              break;

            default:
              HandleUnknownEvent(hmonEvent);
              break;
          }
        } catch (Exception ex) {
          Log.Error(ex, "Error processing HMON event for session {SessionId}.", hmonEvent.SessionId);
        }
      }
    } catch (OperationCanceledException) {
      Log.Information("Event processing loop canceled.");
    } catch (Exception ex) {
      Log.Error(ex, "Unhandled exception in main event processing loop.");
    }

    Log.Information("AdapterService stopping.");
  }
  /// <summary>
  /// Handles received facts from the HMON server, mapping them to session metrics and OTEL attributes.
  /// </summary>
  /// <param name="factsEvent">The facts event received from the HMON server.</param>
  private void HandleFactsReceivedEvent(FactsReceivedEvent factsEvent)
  {
    var sessionId = factsEvent.SessionId != Guid.Empty ? factsEvent.SessionId.ToString() : "default";
    Log.Debug("{session.id} Processing FactsReceivedEvent with {FactCount} facts.",
      sessionId,
      factsEvent.Facts.Facts.Count());

    var sessionAttributes = ExtractSessionAttributes(factsEvent, sessionId);
    var globalTags = sessionAttributes.Select(kv => new KeyValuePair<string, object?>(kv.Key, kv.Value)).ToArray();
    var metrics = _sessionMetrics.GetOrAdd(sessionId, _ => new SessionMetrics { Tags = globalTags });
    metrics.Tags = globalTags;

    foreach (var fact in factsEvent.Facts.Facts) {
      Log.Debug("{session.id} Mapping fact: {FactType}", sessionId, fact.Name);
      UpdateCounters(metrics, fact);
      UpdateSessionMetrics(metrics, fact);
    }
  }

  private void UpdateCounters(SessionMetrics metrics, Fact fact)
  {
    long prevCompactions = metrics.WorkspaceCompactions;
    long prevGcCollections = metrics.WorkspaceGcCollections;
    long prevCpuTime = metrics.AccountCpuTime;
    long prevConnectTime = metrics.AccountConnectTime;
    long prevKeyingTime = metrics.AccountKeyingTime;

    if (fact is WorkspaceFact wsFactLocal) {
      ApplyDelta(prevCompactions, wsFactLocal.Compactions, compactionsCounter, metrics.Tags);
      ApplyDelta(prevGcCollections, wsFactLocal.GarbageCollections, gcCollectionsCounter, metrics.Tags);
    } else if (fact is AccountInformationFact accFactLocal) {
      ApplyDelta(prevCpuTime, accFactLocal.ComputeTime, cpuTimeCounter, metrics.Tags);
      ApplyDelta(prevConnectTime, accFactLocal.ConnectTime, connectTimeCounter, metrics.Tags);
      ApplyDelta(prevKeyingTime, accFactLocal.KeyingTime, keyingTimeCounter, metrics.Tags);
    }
  }

  private Dictionary<string, object?> ExtractSessionAttributes(FactsReceivedEvent factsEvent, string sessionId)
  {
    var hostFact = factsEvent.Facts.Facts.OfType<HostFact>().FirstOrDefault();
    var accFactGlobal = factsEvent.Facts.Facts.OfType<AccountInformationFact>().FirstOrDefault();
    var wsFactGlobal = factsEvent.Facts.Facts.OfType<WorkspaceFact>().FirstOrDefault();

    var sessionAttributes = new Dictionary<string, object?> {
      ["service.name"] = _adapterConfig?.ServiceName ?? "HMON-to-OTEL Adapter",
      ["session.id"] = sessionId
    };

    if (hostFact != null) {
      sessionAttributes["host.name"] = hostFact.Machine.Name;
      sessionAttributes["process.owner"] = hostFact.Machine.User;
      sessionAttributes["process.pid"] = hostFact.Machine.PID;
      sessionAttributes["service.instance.id"] = hostFact.Interpreter.SessionUUID;
      sessionAttributes["service.version"] = hostFact.Interpreter.Version;
      sessionAttributes["dyalog.interpreter.bitwidth"] = hostFact.Interpreter.BitWidth;
      sessionAttributes["dyalog.interpreter.is_unicode"] = hostFact.Interpreter.IsUnicode;
      sessionAttributes["dyalog.interpreter.is_runtime"] = hostFact.Interpreter.IsRuntime;
      sessionAttributes["dyalog.ride.listening"] = hostFact.RIDE?.Listening;
      sessionAttributes["dyalog.ride.address"] = hostFact.RIDE?.Address;
      sessionAttributes["dyalog.conga.version"] = hostFact.CommsLayer?.Version;
      sessionAttributes["dyalog.hmon.access_level"] = hostFact.Machine.AccessLevel;
    }
    if (accFactGlobal != null) {
      sessionAttributes["enduser.id"] = accFactGlobal.UserIdentification.ToString();
      if (!string.IsNullOrEmpty(accFactGlobal.UserIdentification.ToString()))
        sessionAttributes["user_id"] = accFactGlobal.UserIdentification.ToString();
    }
    if (wsFactGlobal != null && !string.IsNullOrEmpty(wsFactGlobal.WSID))
      sessionAttributes["wsid"] = wsFactGlobal.WSID;

    return sessionAttributes;
  }

  private static void UpdateSessionMetrics(SessionMetrics metrics, Fact fact)
  {
    if (fact is WorkspaceFact wsFactLocal) {
      metrics.WorkspaceMemoryAvailable = wsFactLocal.Available;
      metrics.WorkspaceMemoryUsed = wsFactLocal.Used;
      metrics.WorkspaceMemoryAllocation = wsFactLocal.Allocation;
      metrics.WorkspaceMemoryAllocationHwm = wsFactLocal.AllocationHWM;
      metrics.WorkspaceCompactions = wsFactLocal.Compactions;
      metrics.WorkspaceGcCollections = wsFactLocal.GarbageCollections;
      metrics.WorkspacePocketsGarbage = wsFactLocal.GarbagePockets;
      metrics.WorkspacePocketsFree = wsFactLocal.FreePockets;
      metrics.WorkspacePocketsUsed = wsFactLocal.UsedPockets;
      metrics.WorkspaceSediment = wsFactLocal.Sediment;
      metrics.WorkspaceTrapReserveWanted = wsFactLocal.TrapReserveWanted;
      metrics.WorkspaceTrapReserveActual = wsFactLocal.TrapReserveActual;
    } else if (fact is AccountInformationFact accFactLocal) {
      metrics.AccountCpuTime = accFactLocal.ComputeTime;
      metrics.AccountConnectTime = accFactLocal.ConnectTime;
      metrics.AccountKeyingTime = accFactLocal.KeyingTime;
    } else if (fact is ThreadCountFact threadCountFactLocal) {
      metrics.ThreadsTotal = threadCountFactLocal.Total;
      metrics.ThreadsSuspended = threadCountFactLocal.Suspended;
    }
  }

  private static void ApplyDelta(long previous, long current, Counter<long>? counter, KeyValuePair<string, object?>[]? tags)
  {
    long delta = current - previous;
    if (delta > 0 && counter != null)
      counter.Add(delta, tags ?? []);
  }
  /// <summary>
  /// Handles notification events from the HMON server, enriching logs with event-specific context.
  /// </summary>
  /// <param name="notificationEvent">The notification event received.</param>
  /// <param name="stoppingToken">Cancellation token for graceful shutdown.</param>
  private async Task HandleNotificationReceivedEventAsync(NotificationReceivedEvent notificationEvent, CancellationToken stoppingToken)
  {
    var sessionId = notificationEvent.SessionId;
    var logAttributes = new Dictionary<string, object> {
      ["service.name"] = _adapterConfig.ServiceName,
      ["session.id"] = sessionId,
      ["notification.uid"] = notificationEvent.Notification.UID
    };
    var eventName = notificationEvent.Notification.Event?.Name ?? "Unknown";
    Log.Debug("{session.id} Processing NotificationReceivedEvent with event {event.name}",
      sessionId, eventName);

    MergeSessionTagsIntoAttributes(sessionId.ToString(), logAttributes);
    switch (eventName) {
      case "UntrappedSignal":
      case "TrappedSignal":
        var n = notificationEvent.Notification;
        var dmx = n.DMX;
        logAttributes["notification.exception"] = n.Exception;
        logAttributes["dmx.restricted"] = dmx.Restricted;
        logAttributes["dmx.category"] = dmx.Category;
        logAttributes["dmx.dm"] = dmx.DM;
        logAttributes["dmx.em"] = dmx.EM;
        logAttributes["dmx.en"] = dmx.EN;
        logAttributes["dmx.enx"] = dmx.ENX;
        logAttributes["dmx.internal_location"] = dmx.InternalLocation;
        logAttributes["dmx.vendor"] = dmx.Vendor;
        logAttributes["dmx.message"] = dmx.Message;
        logAttributes["dmx.os_error"] = dmx.OSError;
        logAttributes["dmx.os_error.source"] = dmx.OSError?.Source;
        logAttributes["dmx.os_error.code"] = dmx.OSError?.Code;
        logAttributes["dmx.os_error.description"] = dmx.OSError?.Description;

        _otelLogger.LogErrorWithContext(logAttributes,
            "Event received {event.name} {dmx.category} '{dmx.message}' {dyalog.signal.stack} {dyalog.signal.thread_info}",
            eventName,
            dmx?.Category,
            dmx?.Message,
            n.Stack,
            n.Tid);
        break;
      case "WorkspaceResize":
        _otelLogger.LogInformationWithContext(logAttributes,
            "Event received {event.name} {resize.new_size}",
            eventName,
            notificationEvent.Notification.Size);
        break;
      default:
        _otelLogger.LogInformationWithContext(logAttributes,
            "{event.name} event received", "Notification");
        break;
    }
  }
  /// <summary>
  /// Handles user message events from the HMON server and logs them with context.
  /// </summary>
  /// <param name="userMsgEvent">The user message event received.</param>
  private void HandleUserMessageReceivedEvent(UserMessageReceivedEvent userMsgEvent)
  {
    var sessionId = userMsgEvent.SessionId != Guid.Empty ? userMsgEvent.SessionId.ToString() : "default";
    Log.Debug("{session.id} Processing NotificationReceivedEvent with event {event.name}",
      sessionId, "UserMessage");

    var logAttributes = new Dictionary<string, object> {
      ["service.name"] = _adapterConfig.ServiceName,
      ["session.id"] = sessionId,
      ["user_message.uid"] = userMsgEvent.Message?.UID
    };
    MergeSessionTagsIntoAttributes(sessionId, logAttributes);
    _otelLogger.LogInformationWithContext(logAttributes, "{event.name} received: {user_message}",
      "UserMessageReceived",
      userMsgEvent.Message?.Message.GetRawText());
  }
  /// <summary>
  /// Merges session-level tags into the provided log attribute dictionary for enriched logging.
  /// </summary>
  /// <param name="sessionId">Session identifier.</param>
  /// <param name="logAttributes">Dictionary of log attributes to enrich.</param>
  private void MergeSessionTagsIntoAttributes(string sessionId, Dictionary<string, object> logAttributes)
  {
    if (_sessionMetrics.TryGetValue(sessionId, out var metrics) && metrics.Tags != null) {
      foreach (var tag in metrics.Tags) {
        if (!logAttributes.ContainsKey(tag.Key) && tag.Value != null)
          logAttributes[tag.Key] = tag.Value;
      }
    }
  }
  /// <summary>
  /// Handles unknown or unrecognized HMON events, logging them for diagnostics.
  /// </summary>
  /// <param name="hmonEvent">The unknown event object.</param>
  private void HandleUnknownEvent(object hmonEvent)
  {
    var eventType = hmonEvent?.GetType().Name ?? "*unknown*";
    Log.Warning("Received unknown HMON event type: {EventType} {@Event}", eventType, hmonEvent);
    var logAttributes = new Dictionary<string, object> {
      ["service.name"] = _adapterConfig.ServiceName,
      ["event.type"] = eventType,
      ["event.payload"] = hmonEvent?.ToString() ?? "(null)"
    };
    _otelLogger.LogWarningWithContext(logAttributes,
      "Unknown HMON event received {event.type} {event.payload}", eventType, hmonEvent?.ToString() ?? "(null)"
      );
  }
  /// <summary>
  /// Disposes resources used by the AdapterService, including orchestrator and telemetry providers.
  /// </summary>
  /// <returns>A task representing the asynchronous dispose operation.</returns>
  public async ValueTask DisposeAsync()
  {
    if (_orchestrator is not null)
      await _orchestrator.DisposeAsync();
    _meter?.Dispose();
    _telemetryFactory?.MeterProvider?.Dispose();
  }
}
