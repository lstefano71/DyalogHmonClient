using Dyalog.Hmon.Client.Lib;

namespace Dyalog.Hmon.HubSample.Web;

public class HmonHubOrchestratorService : IAsyncDisposable
{
  private readonly HmonOrchestrator _orchestrator;
  private readonly FactAggregator _aggregator;
  private readonly List<HmonServerConfig> _servers;
  private readonly CancellationTokenSource _cts = new();
  private readonly WebSocketHub? _wsHub;
  private readonly PollListenerConfig? _pollListener;
  private readonly List<FactType> _pollFactTypes;
  private readonly int _pollIntervalSeconds;
  private readonly int _historySize;
  private readonly List<SubscriptionEvent>? _eventEnums;
  private static readonly Dictionary<string, FactType> FactTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "host", FactType.Host },
        { "accountinformation", FactType.AccountInformation },
        { "workspace", FactType.Workspace },
        { "threads", FactType.Threads },
        { "suspendedthreads", FactType.SuspendedThreads },
        { "threadcount", FactType.ThreadCount }
    };

  public HmonHubOrchestratorService(HubSampleConfig config, FactAggregator aggregator, WebSocketHub? wsHub = null)
  {
    _aggregator = aggregator;
    _servers = config.HmonServers;
    _wsHub = wsHub;
    _pollListener = config.PollListener;
    _pollFactTypes = config.PollFacts?
        .Select(f => FactTypeMap.TryGetValue(f, out var ft) ? ft : (FactType?)null)
        .Where(ft => ft.HasValue)
        .Select(ft => ft!.Value)
        .ToList() ?? [FactType.Host, FactType.Threads];
    _pollIntervalSeconds = config.PollIntervalSeconds ?? 5;
    _historySize = config.EventHistorySize ?? 10;

    // Map string event names to SubscriptionEvent enum
    _eventEnums = config.EventSubscription?
        .Select(name => Enum.TryParse<SubscriptionEvent>(name, ignoreCase: true, out var ev) ? ev : (SubscriptionEvent?)null)
        .Where(ev => ev.HasValue)
        .Select(ev => ev!.Value)
        .ToList() ?? [SubscriptionEvent.UntrappedSignal];

    _orchestrator = new HmonOrchestrator(new HmonOrchestratorOptions {
      // Add options as needed (e.g., retry policy)
    });

    // Subscribe to client connected event for both SERVE and POLL
    _orchestrator.ClientConnected += async args => {
      try {
        await _orchestrator.SubscribeAsync(args.SessionId, _eventEnums);

        await _orchestrator.PollFactsAsync(args.SessionId, _pollFactTypes, TimeSpan.FromSeconds(_pollIntervalSeconds));
      } catch (Exception ex) {
        Serilog.Log.Logger.Error(ex, "Failed to start polling facts for session {SessionId}", args.SessionId);
      }
    };
  }

  public async Task StartAsync()
  {
    // Start listener if pollListener is configured
    if (_pollListener is not null) {
      _ = _orchestrator.StartListenerAsync(_pollListener.Ip, _pollListener.Port, _cts.Token);
    }

    // Connect to all configured servers
    foreach (var server in _servers) {
      _orchestrator.AddServer(server.Host, server.Port, server.Name);
    }

    // Start event loop for unified event stream
    _ = Task.Run(async () => {
      await foreach (var evt in _orchestrator.Events.WithCancellation(_cts.Token)) {
        if (evt is FactsReceivedEvent factsEvent) {
          foreach (var fact in factsEvent.Facts.Facts) {
            _aggregator.UpdateFact(
                factsEvent.SessionId.ToString(),
                factsEvent.SessionId,
                fact.Name,
                fact
            );
            _wsHub?.BroadcastFactUpdate(new FactRecord(
                factsEvent.SessionId.ToString(),
                factsEvent.SessionId,
                fact.Name,
                fact,
                DateTimeOffset.UtcNow
            ));
          }
        } else if (evt is NotificationReceivedEvent notificationEvent) {
          var sessionId = notificationEvent.SessionId;
          var eventName = notificationEvent.Notification.Event.Name;
          var payload = notificationEvent.Notification;
          var timestamp = DateTimeOffset.UtcNow;

          _aggregator.AddEvent(
              serverName: sessionId.ToString(),
              sessionId: sessionId,
              eventName: eventName,
              payload: payload,
              timestamp: timestamp
          );

          // Immediately send event through websocket
          _wsHub?.BroadcastEvent(
              serverName: sessionId.ToString(),
              sessionId: sessionId,
              eventName: eventName,
              payload: payload,
              timestamp: timestamp
          );
        }
        // Optionally handle disconnects, errors, etc.
      }
    }, _cts.Token);
  }

  public async ValueTask DisposeAsync()
  {
    _cts.Cancel();
    await _orchestrator.DisposeAsync();
  }
}
