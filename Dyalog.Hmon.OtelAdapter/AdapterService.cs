using Dyalog.Hmon.Client.Lib;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

using Serilog;

using Spectre.Console;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;

namespace Dyalog.Hmon.OtelAdapter;

public class AdapterService : BackgroundService, IAsyncDisposable
{
  private AdapterConfig? _adapterConfig;
  private HmonOrchestrator? _orchestrator;
  private TelemetryFactory? _telemetryFactory;
  private Meter? _meter;
  private Dictionary<string, object>? _instrumentCache;
  private readonly System.Collections.Concurrent.ConcurrentDictionary<string, MeterProvider> _sessionMeterProviders = new();

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    Log.Information("AdapterService started.");
    AnsiConsole.MarkupLine("[bold blue]AdapterService running.[/]");

    _adapterConfig = LoadAndValidateConfig();
    if (_adapterConfig == null)
    {
      return;
    }

    Log.Debug("Loaded configuration: {@AdapterConfig}", _adapterConfig);
    Log.Information("OTLP Endpoint: {Endpoint}", _adapterConfig.OtelExporter?.Endpoint ?? "(null)");

    _orchestrator = new HmonOrchestrator();
    _telemetryFactory = new TelemetryFactory(_adapterConfig);

    _orchestrator.ClientConnected += async args =>
    {
      // WARNING: If FactType enum changes, update this list accordingly.

      var pollingInterval = TimeSpan.FromMilliseconds(_adapterConfig?.PollingIntervalMs ?? 1000);

      await _orchestrator.PollFactsAsync(args.SessionId,
      [
        FactType.Host,
        FactType.AccountInformation,
        FactType.Workspace,
        FactType.Threads,
        FactType.SuspendedThreads,
        FactType.ThreadCount
      ], pollingInterval);
      Log.Information("Started polling for FactTypes: {FactTypes} on session {SessionId} with interval {Interval}ms", string.Join(",",
      [
        FactType.Host,
        FactType.AccountInformation,
        FactType.Workspace,
        FactType.Threads,
        FactType.SuspendedThreads,
        FactType.ThreadCount
      ]), args.SessionId, pollingInterval.TotalMilliseconds);

      // Subscribe to all events for the session
      await _orchestrator.SubscribeAsync(args.SessionId, [SubscriptionEvent.All]);
      Log.Information("Subscribed to all events on session {SessionId}", args.SessionId);
    };

    foreach (var server in _adapterConfig.HmonServers)
    {
      try
      {
        _orchestrator.AddServer(server.Host, server.Port, server.Name);
        Log.Information("Added HMON server: {Host}:{Port} ({Name})", server.Host, server.Port, server.Name ?? "");
      }
      catch (Exception ex)
      {
        var logAttributes = new Dictionary<string, object>
        {
          ["event.name"] = "ConnectionFailed",
          ["net.peer.name"] = server.Host,
          ["net.peer.port"] = server.Port,
          ["error.message"] = ex.Message
        };
        Log.Error(ex, "Failed to connect to HMON server [{Attributes}]", System.Text.Json.JsonSerializer.Serialize(logAttributes));
      }
    }

    // TODO: Optionally start listener if required by config

    _meter = new Meter("HMON");
    _instrumentCache = [];

    _orchestrator.ClientDisconnected += async args =>
    {
      var logAttributes = new Dictionary<string, object>
      {
        ["event.name"] = "ClientDisconnected",
        ["net.peer.name"] = args.Host,
        ["net.peer.port"] = args.Port,
        ["error.message"] = args.Reason,
        ["service.name"] = _adapterConfig.ServiceName,
        ["session.id"] = args.SessionId.ToString()
      };
      Log.Error("HMON client disconnected [{Attributes}]", System.Text.Json.JsonSerializer.Serialize(logAttributes));
      await Task.CompletedTask;
    };

    await ProcessEventsAsync(stoppingToken);

    Log.Information("AdapterService stopping.");
  }

  private async Task ProcessEventsAsync(CancellationToken stoppingToken)
  {
    try
    {
      await foreach (var hmonEvent in _orchestrator.Events.WithCancellation(stoppingToken))
      {
        switch (hmonEvent)
        {
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
      }
    }
    catch (Exception ex)
    {
      Log.Error(ex, "Unhandled exception in main event processing loop");
    }
  }

  private void HandleFactsReceivedEvent(FactsReceivedEvent factsEvent)
  {
    Log.Debug("Processing FactsReceivedEvent with {FactCount} facts.", factsEvent.Facts.Facts.Count());

    // Extract session ID from factsEvent if available (assume factsEvent.SessionId exists)
    var sessionId = factsEvent.SessionId != Guid.Empty ? factsEvent.SessionId.ToString() : "default";
    if (!_sessionMeterProviders.ContainsKey(sessionId))
    {
      var hostFact = factsEvent.Facts.Facts.OfType<HostFact>().FirstOrDefault();
      var accFact = factsEvent.Facts.Facts.OfType<AccountInformationFact>().FirstOrDefault();

      var attributes = new List<KeyValuePair<string, object>>
      {
                new("service.name", _adapterConfig?.ServiceName ?? "HMON-to-OTEL Adapter")
            };

      if (hostFact != null)
      {
        attributes.Add(new KeyValuePair<string, object>("host.name", hostFact.Machine.Name ?? ""));
        attributes.Add(new KeyValuePair<string, object>("host.user", hostFact.Machine.User ?? ""));
        attributes.Add(new KeyValuePair<string, object>("host.pid", hostFact.Machine.PID));
      }
      if (accFact != null)
      {
        attributes.Add(new KeyValuePair<string, object>("user.id", accFact.UserIdentification));
      }

      var meterName = "HMON-" + sessionId;
      var meterProvider = Sdk.CreateMeterProviderBuilder()
          .ConfigureResource(r => r.AddAttributes(attributes))
          .AddMeter(meterName)
          .AddOtlpExporter(options =>
          {
            options.Endpoint = new Uri(_adapterConfig?.OtelExporter?.Endpoint ?? "http://localhost:4317");
          })
          .Build();

      _sessionMeterProviders[sessionId] = meterProvider;
      Log.Information("Created session-specific MeterProvider for session {SessionId} with enriched Resource attributes.", sessionId);
    }

    var meterNameForSession = "HMON-" + sessionId;
    var meterForSession = new Meter(meterNameForSession, null);

    foreach (var fact in factsEvent.Facts.Facts)
    {
      Log.Debug("Mapping fact: {FactType}", fact.GetType().Name);

      if (fact is HostFact hostFact)
      {
        var metricName = "host.pid";
        var value = hostFact.Machine.PID;
        var tags = new List<KeyValuePair<string, object>>
        {
                    new("host", hostFact.Machine.Name ?? ""),
                    new("user", hostFact.Machine.User ?? "")
                };

        if (!_instrumentCache.TryGetValue(metricName, out var instrument))
        {
          instrument = meterForSession.CreateObservableGauge<int>(metricName, () => value, description: "Host PID");
          _instrumentCache[metricName] = instrument;
        }

        Log.Information("Recording metric {MetricName}: {Value} [{Tags}]", metricName, value, string.Join(",", tags.Select(t => $"{t.Key}={t.Value}")));
      }
      else if (fact is WorkspaceFact wsFact)
      {
        var workspaceMetrics = new[]
        {
                    ("workspace.available", wsFact.Available, "Workspace Available"),
                    ("workspace.used", wsFact.Used, "Workspace Used"),
                    ("workspace.compactions", wsFact.Compactions, "Workspace Compactions"),
                    ("workspace.garbage_collections", wsFact.GarbageCollections, "Workspace Garbage Collections"),
                    ("workspace.garbage_pockets", wsFact.GarbagePockets, "Workspace Garbage Pockets"),
                    ("workspace.free_pockets", wsFact.FreePockets, "Workspace Free Pockets"),
                    ("workspace.used_pockets", wsFact.UsedPockets, "Workspace Used Pockets")
                };
        foreach (var (metricName, value, description) in workspaceMetrics)
        {
          var tags = new List<KeyValuePair<string, object>>
          {
                        new("wsid", wsFact.WSID ?? "")
                    };

          if (!_instrumentCache.TryGetValue(metricName, out var instrument))
          {
            instrument = _meter.CreateObservableGauge<long>(metricName, () => value, description: description);
            _instrumentCache[metricName] = instrument;
          }

          Log.Information("Recording metric {MetricName}: {Value} [{Tags}]", metricName, value, string.Join(",", tags.Select(t => $"{t.Key}={t.Value}")));
        }
      }
      else if (fact is AccountInformationFact accFact)
      {
        var metrics = new[]
        {
                    ("account.compute_time", accFact.ComputeTime, "Compute Time"),
                    ("account.connect_time", accFact.ConnectTime, "Connect Time"),
                    ("account.keying_time", accFact.KeyingTime, "Keying Time")
                };
        foreach (var (metricName, value, description) in metrics)
        {
          var tags = new List<KeyValuePair<string, object>>
          {
                        new("user_id", accFact.UserIdentification)
                    };
          if (!_instrumentCache.TryGetValue(metricName, out var instrument))
          {
            instrument = _meter.CreateObservableGauge<long>(metricName, () => value, description: description);
            _instrumentCache[metricName] = instrument;
          }
          Log.Information("Recording metric {MetricName}: {Value} [{Tags}]", metricName, value, string.Join(",", tags.Select(t => $"{t.Key}={t.Value}")));
        }
      }
      else if (fact is HostFact hostFact2)
      {
        var interp = hostFact2.Interpreter;
        var metricName = "interpreter.version";
        var tags = new List<KeyValuePair<string, object>>
        {
                    new("version", interp.Version ?? "")
                };
        Log.Information("Interpreter version: {Version} [{Tags}]", interp.Version, string.Join(",", tags.Select(t => $"{t.Key}={t.Value}")));
      }
      else if (fact is HostFact hostFact3 && hostFact3.CommsLayer is not null)
      {
        var comms = hostFact3.CommsLayer;
        var metrics = new[]
        {
                    ("commslayer.port4", comms.Port4, "CommsLayer IPv4 Port"),
                    ("commslayer.port6", comms.Port6, "CommsLayer IPv6 Port")
                };
        foreach (var (metricName, value, description) in metrics)
        {
          var tags = new List<KeyValuePair<string, object>>
          {
                        new("address", comms.Address ?? ""),
                        new("version", comms.Version ?? "")
                    };
          if (!_instrumentCache.TryGetValue(metricName, out var instrument))
          {
            instrument = _meter.CreateObservableGauge<int>(metricName, () => value, description: description);
            _instrumentCache[metricName] = instrument;
          }
          Log.Information("Recording metric {MetricName}: {Value} [{Tags}]", metricName, value, string.Join(",", tags.Select(t => $"{t.Key}={t.Value}")));
        }
      }
      else if (fact is HostFact hostFact4 && hostFact4.RIDE is not null)
      {
        var ride = hostFact4.RIDE;
        var metricName = "ride.listening";
        var value = ride.Listening ? 1 : 0;
        var tags = new List<KeyValuePair<string, object>>
        {
                    new("ride", "listening")
                };
        if (!_instrumentCache.TryGetValue(metricName, out var instrument))
        {
          instrument = _meter.CreateObservableGauge<int>(metricName, () => value, description: "RIDE Listening");
          _instrumentCache[metricName] = instrument;
        }
        Log.Information("Recording metric {MetricName}: {Value} [{Tags}]", metricName, value, string.Join(",", tags.Select(t => $"{t.Key}={t.Value}")));
      }
    }
  }

  private async Task HandleNotificationReceivedEventAsync(NotificationReceivedEvent notificationEvent, CancellationToken stoppingToken)
  {
    var notif = notificationEvent.Notification;
    var notifType = notif.Event.Name;
    var notifTags = new List<KeyValuePair<string, object>>
    {
      new("uid", notif.UID ?? ""),
      new("event", notifType)
    };

    if (notifType == "UntrappedSignal" || notifType == "TrappedSignal")
    {
      var severity = notifType == "UntrappedSignal" ? "ERROR" : "WARN";
      var logAttributes = new Dictionary<string, object>
      {
        ["event"] = notifType,
        ["uid"] = notif.UID ?? "",
        ["tid"] = notif.Tid
      };

      if (notif.Tid.HasValue)
      {
        Log.Debug("Fetching ThreadsFact for Tid={Tid}", notif.Tid.Value);
        var facts = await _orchestrator.GetFactsAsync(notificationEvent.SessionId, [FactType.Threads], stoppingToken);
        var threadsFact = facts.Facts.OfType<ThreadsFact>().FirstOrDefault();
        var threadInfo = threadsFact?.Values.FirstOrDefault(t => t.Tid == notif.Tid.Value);

        if (threadInfo != null)
        {
          logAttributes["thread.DMX"] = threadInfo.DMX != null ? System.Text.Json.JsonSerializer.Serialize(threadInfo.DMX) : null;
          logAttributes["thread.Stack"] = threadInfo.Stack != null ? System.Text.Json.JsonSerializer.Serialize(threadInfo.Stack) : null;
          logAttributes["thread.Info"] = System.Text.Json.JsonSerializer.Serialize(threadInfo);
        }
      }

      Log.Error("Signal notification: {Event} [{Attributes}]", notifType, System.Text.Json.JsonSerializer.Serialize(logAttributes));
    }
    else if (notifType == "WorkspaceResize")
    {
      var logAttributes = new Dictionary<string, object>
      {
        ["event.name"] = notifType,
        ["dyalog.workspace.size"] = notif.Size
      };
      Log.Information("WorkspaceResize notification: {Event} [{Attributes}]", notifType, System.Text.Json.JsonSerializer.Serialize(logAttributes));
    }
    else
    {
      Log.Information("Notification: {Event} [{Tags}]", notifType, string.Join(",", notifTags.Select(t => $"{t.Key}={t.Value}")));
    }
  }

  private void HandleUserMessageReceivedEvent(UserMessageReceivedEvent userMsgEvent)
  {
    var msg = userMsgEvent.Message;
    var logAttributes = new Dictionary<string, object>
    {
      ["event.name"] = "UserMessage",
      ["dyalog.usermsg.uid"] = msg.UID,
      ["dyalog.usermsg.body"] = System.Text.Json.JsonSerializer.Serialize(msg.Message)
    };
    Log.Information("User message received [{Attributes}]", System.Text.Json.JsonSerializer.Serialize(logAttributes));
  }

  private void HandleUnknownEvent(object hmonEvent)
  {
    Log.Debug("Received HMON event: {EventType}", hmonEvent.GetType().Name);
  }

  private AdapterConfig? LoadAndValidateConfig()
  {
    var config = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("config.json", optional: true)
        .AddEnvironmentVariables()
        .AddCommandLine(Environment.GetCommandLineArgs())
        .Build();

    var adapterConfig = config.Get<AdapterConfig>();
    var validationContext = new ValidationContext(adapterConfig ?? new AdapterConfig());
    var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
    if (adapterConfig == null || !Validator.TryValidateObject(adapterConfig, validationContext, validationResults, true))
    {
      Log.Error("Configuration validation failed: {Errors}", string.Join("; ", validationResults.Select(r => r.ErrorMessage)));
      AnsiConsole.MarkupLine("[bold red]Configuration validation failed.[/]");
      return null;
    }
    return adapterConfig;
  }

  public async ValueTask DisposeAsync()
  {
    if (_orchestrator is not null)
      await _orchestrator.DisposeAsync();

    foreach (var provider in _sessionMeterProviders.Values)
      provider.Dispose();

    _meter?.Dispose();
  }
}
