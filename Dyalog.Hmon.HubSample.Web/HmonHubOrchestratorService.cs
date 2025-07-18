using Dyalog.Hmon.Client.Lib;

using Serilog;
namespace Dyalog.Hmon.HubSample.Web;

public class HmonHubOrchestratorService(HubSampleConfig config, FactAggregator aggregator, WebSocketHub? wsHub = null) : IAsyncDisposable
{
  private readonly HmonOrchestrator _orchestrator = new(new HmonOrchestratorOptions {
    // Add options as needed (e.g., retry policy)
  });
  private readonly FactAggregator _aggregator = aggregator;
  private readonly List<HmonServerConfig> _servers = config.HmonServers ?? [];
  private readonly CancellationTokenSource _cts = new();
  private readonly WebSocketHub? _wsHub = wsHub;
  private readonly PollListenerConfig? _pollListener = config.PollListener;
  private readonly List<FactType> _pollFactTypes = config.PollFacts?
        .Select(f => FactTypeMap.TryGetValue(f, out var ft) ? ft : (FactType?)null)
        .Where(ft => ft.HasValue)
        .Select(ft => ft!.Value)
        .ToList() ?? [FactType.Host, FactType.Threads];
  private readonly int _pollIntervalSeconds = config.PollIntervalSeconds ?? 5;
  private readonly int _historySize = config.EventHistorySize ?? 10;
  private readonly List<SubscriptionEvent>? _eventEnums = config.EventSubscription?
        .Select(name => Enum.TryParse<SubscriptionEvent>(name, ignoreCase: true, out var ev) ? ev : (SubscriptionEvent?)null)
        .Where(ev => ev.HasValue)
        .Select(ev => ev!.Value)
        .ToList() ?? [SubscriptionEvent.UntrappedSignal];
  private static readonly Dictionary<string, FactType> FactTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "host", FactType.Host },
        { "accountinformation", FactType.AccountInformation },
        { "workspace", FactType.Workspace },
        { "threads", FactType.Threads },
        { "suspendedthreads", FactType.SuspendedThreads },
        { "threadcount", FactType.ThreadCount }
    };

  public Task StartAsync()
  {
    // Start listener if pollListener is configured
    if (_pollListener is not null) {
      _ = _orchestrator.StartListenerAsync(_pollListener.Ip, _pollListener.Port, _cts.Token);
    }
    // Connect to all configured servers
    if (_servers != null) {
      foreach (var server in _servers) {
        _orchestrator.AddServer(server.Host, server.Port, server.Name);
      }
    }
    // Start the single, unified event processing loop
    _ = Task.Run(async () => {
      Log.Information("OrchestratorService: Event consumer loop starting...");
      try {
        await foreach (var evt in _orchestrator.Events.WithCancellation(_cts.Token)) {
          var serverName = (evt as SessionConnectedEvent)?.FriendlyName ?? (evt as SessionDisconnectedEvent)?.FriendlyName ?? evt.SessionId.ToString();

          switch (evt) {
            case SessionConnectedEvent connected:
              Log.Information("Hub session connected: {SessionId} ({FriendlyName}) from {Host}:{Port}",
                  connected.SessionId, connected.FriendlyName, connected.Host, connected.Port);
              try {
                await _orchestrator.SubscribeAsync(connected.SessionId, _eventEnums);
                await _orchestrator.PollFactsAsync(connected.SessionId, _pollFactTypes, TimeSpan.FromSeconds(_pollIntervalSeconds));
              } catch (Exception ex) {
                Log.Error(ex, "Failed to start polling facts for session {SessionId}", connected.SessionId);
              }
              break;

            case SessionDisconnectedEvent disconnected:
              Log.Information("Hub session disconnected: {SessionId} ({FriendlyName}). Reason: {Reason}",
                  disconnected.SessionId, disconnected.FriendlyName, disconnected.Reason);
              _aggregator.RemoveSession(serverName, disconnected.SessionId);
              // Optionally broadcast a disconnect event to WebSocket clients
              _wsHub?.BroadcastSessionStatusUpdate(serverName, disconnected.SessionId, "Disconnected", disconnected.Reason);
              break;

            case FactsReceivedEvent factsEvent:
              foreach (var fact in factsEvent.Facts.Facts) {
                _aggregator.UpdateFact(
                    serverName,
                    factsEvent.SessionId,
                    fact.Name,
                    fact
                );
                _wsHub?.BroadcastFactUpdate(new FactRecord(
                    serverName,
                    factsEvent.SessionId,
                    fact.Name,
                    fact,
                    DateTimeOffset.UtcNow
                ));
              }
              break;

            case NotificationReceivedEvent notificationEvent: {
                var eventName = notificationEvent.Notification.Event.Name;
                var payload = notificationEvent.Notification;
                var timestamp = DateTimeOffset.UtcNow;
                _aggregator.AddEvent(
                    serverName: serverName,
                    sessionId: notificationEvent.SessionId,
                    eventName: eventName,
                    payload: payload,
                    timestamp: timestamp
                );
                // Immediately send event through websocket
                _wsHub?.BroadcastEvent(
                    serverName: serverName,
                    sessionId: notificationEvent.SessionId,
                    eventName: eventName,
                    payload: payload,
                    timestamp: timestamp
                );
              }
              break;
            case UserMessageReceivedEvent userMessageReceivedEvent: {
                var eventName = "UserMessage";
                var payload = userMessageReceivedEvent.Message;
                var timestamp = DateTimeOffset.UtcNow;
                _aggregator.AddEvent(
                    serverName: serverName,
                    sessionId: userMessageReceivedEvent.SessionId,
                    eventName: eventName,
                    payload: payload,
                    timestamp: timestamp
                );
                _wsHub?.BroadcastEvent(
                    serverName: serverName,
                    sessionId: userMessageReceivedEvent.SessionId,
                    eventName: eventName,
                    payload: payload,
                    timestamp: timestamp
                );
              }
              break;
          }
        }
        Log.Information("OrchestratorService: Event consumer loop exited normally.");
      } catch (OperationCanceledException) {
        Log.Information("OrchestratorService: Event consumer loop canceled.");
      } catch (Exception ex) {
        Log.Error(ex, "OrchestratorService: Event consumer loop error.");
      } finally {
        Log.Information("OrchestratorService: Event consumer loop finally block reached.");
      }
    }, _cts.Token);

    return Task.CompletedTask;
  }
  public async ValueTask DisposeAsync()
  {
    _cts.Cancel();
    await _orchestrator.DisposeAsync();
  }
}
