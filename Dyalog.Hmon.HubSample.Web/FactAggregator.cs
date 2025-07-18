using System.Collections.Concurrent;

namespace Dyalog.Hmon.HubSample.Web;

public record FactRecord(
    string ServerName,
    Guid SessionId,
    string FactName,
    object? Value,
    DateTimeOffset LastUpdate
);

public record EventRecord(
    string ServerName,
    Guid SessionId,
    string EventName,
    object? Payload,
    DateTimeOffset Timestamp
);

/// <summary>
/// Aggregates facts and event history for HMON sessions in the hub sample web application.
/// </summary>
public class FactAggregator
{
  private readonly ConcurrentDictionary<(string ServerName, Guid SessionId, string FactName), FactRecord> _facts = new();
  private readonly ConcurrentDictionary<Guid, ConcurrentQueue<EventRecord>> _eventHistory = new();
  private int _maxEvents = 10;

  public IEnumerable<FactRecord> GetAllFacts() => _facts.Values;

  public IEnumerable<EventRecord> GetAllEvents()
  {
    return _eventHistory.Values.SelectMany(q => q);
  }

  public void SetMaxEvents(int maxEvents)
  {
    _maxEvents = maxEvents;
  }

  public void AddEvent(string serverName, Guid sessionId, string eventName, object? payload, DateTimeOffset timestamp)
  {
    var queue = _eventHistory.GetOrAdd(sessionId, _ => new ConcurrentQueue<EventRecord>());
    queue.Enqueue(new EventRecord(serverName, sessionId, eventName, payload, timestamp));
    while (queue.Count > _maxEvents && queue.TryDequeue(out _)) { }
  }

  public void UpdateFact(string serverName, Guid sessionId, string factName, object? value)
  {
    var now = DateTimeOffset.UtcNow;
    var key = (serverName, sessionId, factName);
    _facts.AddOrUpdate(
        key,
        _ => new FactRecord(serverName, sessionId, factName, value, now),
        (_, existing) => existing with { Value = value, LastUpdate = now }
    );
  }

  public void RemoveSession(string serverName, Guid sessionId)
  {
    var keys = _facts.Keys.Where(k => k.ServerName == serverName && k.SessionId == sessionId).ToList();
    foreach (var key in keys)
      _facts.TryRemove(key, out _);
    _eventHistory.TryRemove(sessionId, out _);
  }
}
