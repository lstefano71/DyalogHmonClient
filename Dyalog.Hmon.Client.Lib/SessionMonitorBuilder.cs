namespace Dyalog.Hmon.Client.Lib;

public class SessionMonitorBuilder(HmonOrchestrator orchestrator, Guid sessionId)
{
  private readonly HmonOrchestrator _orchestrator = orchestrator;
  private readonly Guid _sessionId = sessionId;
  private readonly List<SubscriptionEvent> _subscriptions = [];
  private readonly List<FactType> _pollFacts = [];
  private TimeSpan? _pollInterval;
  private readonly List<IDisposable> _handlers = [];
  private Func<Fact, Task>? _onFactChanged;
  private Func<HmonEvent, Task>? _onEvent;
  private CancellationToken _cancellationToken = CancellationToken.None;

  public SessionMonitorBuilder SubscribeTo(params SubscriptionEvent[] events)
  {
    _subscriptions.AddRange(events);
    return this;
  }

  public SessionMonitorBuilder PollFacts(TimeSpan interval, params FactType[] facts)
  {
    _pollInterval = interval;
    _pollFacts.AddRange(facts);
    return this;
  }

  public SessionMonitorBuilder OnFactChanged(Func<Fact, Task> handler)
  {
    _onFactChanged = handler;
    return this;
  }

  public SessionMonitorBuilder OnEvent(Func<HmonEvent, Task> handler)
  {
    _onEvent = handler;
    return this;
  }

  public SessionMonitorBuilder WithCancellation(CancellationToken ct)
  {
    _cancellationToken = ct;
    return this;
  }

  public async Task StartAsync()
  {
    if (_subscriptions.Count > 0)
      await _orchestrator.SubscribeAsync(_sessionId, _subscriptions, null, _cancellationToken);

    if (_pollFacts.Count > 0 && _pollInterval.HasValue)
      await _orchestrator.PollFactsAsync(_sessionId, _pollFacts, _pollInterval.Value, null, _cancellationToken);

    // Attach event handlers
    var eventTask = Task.Run(async () => {
      await foreach (var evt in _orchestrator.Events) {
        if (evt.SessionId != _sessionId)
          continue;

        if (_onEvent != null)
          await _onEvent(evt);

        if (_onFactChanged != null && evt is FactsReceivedEvent factsEvt) {
          foreach (var fact in factsEvt.Facts.Facts)
            await _onFactChanged(fact);
        }
      }
    }, _cancellationToken);

    // Optionally return a disposable to stop monitoring
  }
}
