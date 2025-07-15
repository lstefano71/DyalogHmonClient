using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;

namespace Dyalog.Hmon.HubSample.Web;

public class WebSocketHub(FactAggregator aggregator)
{
  private readonly FactAggregator _aggregator = aggregator;
  private readonly List<WebSocket> _clients = [];
  private readonly Channel<object> _updates = Channel.CreateUnbounded<object>();

  public async Task HandleWebSocketAsync(HttpContext context)
  {
    if (!context.WebSockets.IsWebSocketRequest) {
      context.Response.StatusCode = 400;
      return;
    }

    using var ws = await context.WebSockets.AcceptWebSocketAsync();
    lock (_clients) _clients.Add(ws);

    // Send initial snapshot
    var snapshot = new {
      type = "snapshot",
      payload = new { facts = _aggregator.GetAllFacts() }
    };
    await ws.SendAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot), WebSocketMessageType.Text, true, context.RequestAborted);

    // Listen for updates
    var reader = _updates.Reader;
    var sendTask = Task.Run(async () => {
      await foreach (var update in reader.ReadAllAsync(context.RequestAborted)) {
        if (ws.State == WebSocketState.Open) {
          await ws.SendAsync(JsonSerializer.SerializeToUtf8Bytes(update), WebSocketMessageType.Text, true, context.RequestAborted);
        }
      }
    });

    // Keep connection open until closed
    var buffer = new byte[1024];
    while (ws.State == WebSocketState.Open) {
      var result = await ws.ReceiveAsync(buffer, context.RequestAborted);
      if (result.MessageType == WebSocketMessageType.Close)
        break;
    }

    lock (_clients) _clients.Remove(ws);
  }

  public void BroadcastFactUpdate(FactRecord record)
  {
    var update = new {
      type = "update",
      payload = new {
        serverName = record.ServerName,
        sessionId = record.SessionId,
        fact = new {
          name = record.FactName,
          value = record.Value,
          lastUpdate = record.LastUpdate
        }
      }
    };
    _updates.Writer.TryWrite(update);
  }

  public void BroadcastEvent(string serverName, Guid sessionId, string eventName, object? payload, DateTimeOffset timestamp)
  {
    var evt = new {
      type = "event",
      payload = new {
        serverName,
        sessionId,
        eventName,
        payload,
        timestamp
      }
    };
    _updates.Writer.TryWrite(evt);
  }
}
