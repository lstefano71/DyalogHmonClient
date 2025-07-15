using Serilog;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace Dyalog.Hmon.Client.Lib;

/// <summary>
/// Central orchestrator for managing HMON connections and exposing a unified event stream.
/// </summary>
public class HmonOrchestrator(HmonOrchestratorOptions? options = null) : IAsyncDisposable
{
  private readonly HmonOrchestratorOptions _options = options ?? new HmonOrchestratorOptions();
  private readonly Channel<HmonEvent> _eventChannel = Channel.CreateUnbounded<HmonEvent>();
  private readonly ConcurrentDictionary<Guid, HmonConnection> _connections = new();
  private readonly ConcurrentDictionary<Guid, ServerConnection> _servers = new();

  // Fact cache: (sessionId, factType) -> FactCacheEntry
  private readonly ConcurrentDictionary<(Guid, Type), FactCacheEntry> _factCache = new();

  public readonly record struct FactCacheEntry(Fact Fact, DateTimeOffset LastUpdated);

  private int _disposeCalled = 0;

  /// <summary>
  /// Unified asynchronous event stream for all HMON events.
  /// </summary>
  public IAsyncEnumerable<HmonEvent> Events => WatchAndCacheFacts();

  /// <summary>
  /// Event fired when any fact for a session is updated.
  /// </summary>
  public event Action<Guid, Fact>? OnSessionUpdated;

  // Internal: watches event stream and updates fact cache
  private async IAsyncEnumerable<HmonEvent> WatchAndCacheFacts([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
  {
    await foreach (var evt in _eventChannel.Reader.ReadAllAsync(ct)) {
      if (evt is FactsReceivedEvent factsEvt) {
        foreach (var fact in factsEvt.Facts.Facts) {
          if (fact != null) {
            _factCache[(evt.SessionId, fact.GetType())] = new FactCacheEntry(fact, DateTimeOffset.UtcNow);
            OnSessionUpdated?.Invoke(evt.SessionId, fact);
          }
        }
      }
      yield return evt;
    }
  }

  /// <summary>
  /// Event fired when a client connects.
  /// </summary>
  public event Func<ClientConnectedEventArgs, Task>? ClientConnected;
  /// <summary>
  /// Event fired when a client disconnects.
  /// </summary>
  public event Func<ClientDisconnectedEventArgs, Task>? ClientDisconnected;

  /// <summary>
  /// Event fired when an error or diagnostic event occurs.
  /// </summary>
  public event Action<Exception, Guid?>? OnError;

  /// <summary>
  /// Starts a TCP listener for incoming HMON connections (POLL mode).
  /// </summary>
  /// <param name="host">Host address to bind.</param>
  /// <param name="port">Port to bind.</param>
  /// <param name="ct">Cancellation token.</param>
  public Task StartListenerAsync(string host, int port, CancellationToken ct = default)
  {
    return Task.Run(async () => {
      var listener = new TcpListener(IPAddress.Parse(host), port);
      listener.Start();
      ct.Register(() => listener.Stop());

      var logger = Log.Logger.ForContext<HmonOrchestrator>();
      try {
        while (!ct.IsCancellationRequested) {
          try {
            var tcpClient = await listener.AcceptTcpClientAsync(ct);
            var sessionId = Guid.NewGuid();
            logger.Debug("Accepted new connection (SessionId={SessionId})", sessionId);
            var remoteEndPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint!;
            var connection = new HmonConnection(
                tcpClient,
                sessionId,
                _eventChannel.Writer,
                async () => {
                  logger.Debug("Connection closed (SessionId={SessionId})", sessionId);
                  _connections.TryRemove(sessionId, out _);
                  if (ClientDisconnected != null) {
                    await ClientDisconnected.Invoke(new ClientDisconnectedEventArgs(sessionId, remoteEndPoint.Address.ToString(), remoteEndPoint.Port, null, "Remote client disconnected"));
                  }
                },
                (args) => { if (ClientConnected != null) return ClientConnected.Invoke(args); return Task.CompletedTask; } // Pass the ClientConnected event handler
            );
            _connections.TryAdd(sessionId, connection);

            try {
              await connection.InitializeAsync(remoteEndPoint.Address.ToString(), remoteEndPoint.Port, null, ct);
            } catch (Exception ex) {
              logger.Error(ex, "Connection initialization failed (SessionId={SessionId}), cleaning up connection", sessionId);
              _connections.TryRemove(sessionId, out _);
              await connection.DisposeAsync();
              OnError?.Invoke(ex, sessionId);
              continue;
            }
          } catch (Exception ex) {
            logger.Error(ex, "Listener loop error");
            OnError?.Invoke(ex, null);
            continue;
          }
        }
      } catch (OperationCanceledException) {
        logger.Debug("Listener canceled");
        // Expected when the cancellation token is triggered.
      } finally {
        logger.Information("Listener stopped");
        listener.Stop();
      }
    }, ct);
  }

  /// <summary>
  /// Adds a remote HMON server (SERVE mode) and starts connection management.
  /// </summary>
  /// <param name="host">Remote host address.</param>
  /// <param name="port">Remote port.</param>
  /// <param name="friendlyName">Optional friendly name.</param>
  /// <returns>Session ID for the server connection.</returns>
  public Guid AddServer(string host, int port, string? friendlyName = null)
  {
    var sessionId = Guid.NewGuid();
    var server = new ServerConnection(
        host, port, friendlyName, _options, _eventChannel.Writer, sessionId,
        (args) => ClientConnected?.Invoke(args),
        (args) => ClientDisconnected?.Invoke(args),
        (conn) => _connections.TryAdd(sessionId, conn)
    );
    _servers.TryAdd(sessionId, server);
    return sessionId;
  }

  /// <summary>
  /// Removes and disposes a server connection by session ID.
  /// </summary>
  /// <param name="sessionId">Session ID to remove.</param>
  public async Task RemoveServerAsync(Guid sessionId)
  {
    if (_servers.TryRemove(sessionId, out var server)) {
      await server.DisposeAsync();
    }
  }

  /// <summary>
  /// Requests a one-time snapshot of facts from the interpreter.
  /// </summary>
  /// <param name="sessionId">Session ID.</param>
  /// <param name="facts">Facts to retrieve.</param>
  /// <param name="ct">Cancellation token.</param>
  public async Task<FactsResponse> GetFactsAsync(Guid sessionId, IEnumerable<FactType> facts, CancellationToken ct = default)
  {
    if (!_connections.TryGetValue(sessionId, out var conn)) {
      var ex = new InvalidOperationException("Session not found");
      Serilog.Log.Logger.ForContext<HmonOrchestrator>().Error("GetFactsAsync: Session not found for sessionId={SessionId}", sessionId);
      OnError?.Invoke(ex, sessionId);
      throw ex;
    }
    var payload = new GetFactsPayload([.. facts.Select(f => (int)f)]);
    try {
      var evt = await conn.SendCommandAsync<FactsReceivedEvent>("GetFacts", payload, ct);
      return evt.Facts;
    } catch (Exception ex) {
      OnError?.Invoke(ex, sessionId);
      throw;
    }
  }

  /// <summary>
  /// Requests a high-priority status report from the interpreter.
  /// </summary>
  /// <param name="sessionId">Session ID.</param>
  /// <param name="ct">Cancellation token.</param>
  public async Task<LastKnownStateResponse> GetLastKnownStateAsync(Guid sessionId, CancellationToken ct = default)
  {
    if (!_connections.TryGetValue(sessionId, out var conn)) {
      var ex = new InvalidOperationException("Session not found");
      OnError?.Invoke(ex, sessionId);
      throw ex;
    }
    var payload = new LastKnownStatePayload();
    try {
      var evt = await conn.SendCommandAsync<LastKnownStateReceivedEvent>("GetLastKnownState", payload, ct);
      return evt.State;
    } catch (Exception ex) {
      OnError?.Invoke(ex, sessionId);
      throw;
    }
  }

  /// <summary>
  /// Starts polling facts from the interpreter at a given interval.
  /// </summary>
  /// <param name="sessionId">Session ID.</param>
  /// <param name="facts">Facts to poll.</param>
  /// <param name="interval">Polling interval.</param>
  /// <param name="ct">Cancellation token.</param>
  public async Task PollFactsAsync(Guid sessionId, IEnumerable<FactType> facts, TimeSpan interval, CancellationToken ct = default)
  {
    if (!_connections.TryGetValue(sessionId, out var conn)) {
      var ex = new InvalidOperationException("Session not found");
      OnError?.Invoke(ex, sessionId);
      throw ex;
    }
    var payload = new PollFactsPayload([.. facts.Select(f => (int)f)], (int)interval.TotalMilliseconds);
    try {
      await conn.SendCommandAsync<FactsReceivedEvent>("PollFacts", payload, ct);
    } catch (Exception ex) {
      OnError?.Invoke(ex, sessionId);
      throw;
    }
  }

  /// <summary>
  /// Stops any active facts polling for the given session.
  /// </summary>
  /// <param name="sessionId">Session ID.</param>
  /// <param name="ct">Cancellation token.</param>
  public async Task StopFactsPollingAsync(Guid sessionId, CancellationToken ct = default)
  {
    if (!_connections.TryGetValue(sessionId, out var conn)) {
      var ex = new InvalidOperationException("Session not found");
      OnError?.Invoke(ex, sessionId);
      throw ex;
    }
    var payload = new { };
    try {
      await conn.SendCommandAsync<FactsReceivedEvent>("StopFacts", payload, ct);
    } catch (Exception ex) {
      OnError?.Invoke(ex, sessionId);
      throw;
    }
  }

  /// <summary>
  /// Triggers an immediate facts message from an active poll.
  /// </summary>
  /// <param name="sessionId">Session ID.</param>
  /// <param name="ct">Cancellation token.</param>
  public async Task BumpFactsAsync(Guid sessionId, CancellationToken ct = default)
  {
    if (!_connections.TryGetValue(sessionId, out var conn)) {
      var ex = new InvalidOperationException("Session not found");
      OnError?.Invoke(ex, sessionId);
      throw ex;
    }
    var payload = new { };
    try {
      await conn.SendCommandAsync<FactsReceivedEvent>("BumpFacts", payload, ct);
    } catch (Exception ex) {
      OnError?.Invoke(ex, sessionId);
      throw;
    }
  }

  /// <summary>
  /// Subscribes to interpreter events for the given session.
  /// </summary>
  /// <param name="sessionId">Session ID.</param>
  /// <param name="events">Events to subscribe to.</param>
  /// <param name="ct">Cancellation token.</param>
  public async Task SubscribeAsync(Guid sessionId, IEnumerable<SubscriptionEvent> events, CancellationToken ct = default)
  {
    if (!_connections.TryGetValue(sessionId, out var conn)) {
      var ex = new InvalidOperationException("Session not found");
      OnError?.Invoke(ex, sessionId);
      throw ex;
    }
    var payload = new SubscribePayload([.. events.Select(e => (int)e)]);
    try {
      await conn.SendCommandAsync<SubscribedResponseReceivedEvent>("Subscribe", payload, ct);
    } catch (Exception ex) {
      OnError?.Invoke(ex, sessionId);
      throw;
    }
  }

  /// <summary>
  /// Requests the interpreter to connect to a RIDE client.
  /// </summary>
  /// <param name="sessionId">Session ID.</param>
  /// <param name="address">RIDE address.</param>
  /// <param name="port">RIDE port.</param>
  /// <param name="ct">Cancellation token.</param>
  public async Task<RideConnectionResponse> ConnectRideAsync(Guid sessionId, string address, int port, CancellationToken ct = default)
  {
    if (!_connections.TryGetValue(sessionId, out var conn)) {
      var ex = new InvalidOperationException("Session not found");
      OnError?.Invoke(ex, sessionId);
      throw ex;
    }
    var payload = new {
      Address = address,
      Port = port,
      UID = Guid.NewGuid().ToString()
    };
    try {
      var evt = await conn.SendCommandAsync<RideConnectionReceivedEvent>("ConnectRide", payload, ct);
      return evt.Response;
    } catch (Exception ex) {
      OnError?.Invoke(ex, sessionId);
      throw;
    }
  }

  /// <summary>
  /// Requests the interpreter to disconnect from any RIDE client.
  /// </summary>
  /// <param name="sessionId">Session ID.</param>
  /// <param name="ct">Cancellation token.</param>
  public async Task<RideConnectionResponse> DisconnectRideAsync(Guid sessionId, CancellationToken ct = default)
  {
    if (!_connections.TryGetValue(sessionId, out var conn)) {
      var ex = new InvalidOperationException("Session not found");
      OnError?.Invoke(ex, sessionId);
      throw ex;
    }
    var payload = new { };
    try {
      var evt = await conn.SendCommandAsync<RideConnectionReceivedEvent>("ConnectRide", payload, ct);
      return evt.Response;
    } catch (Exception ex) {
      OnError?.Invoke(ex, sessionId);
      throw;
    }
  }

  /// <summary>
  /// Disposes all managed connections and resources.
  /// </summary>
  public async ValueTask DisposeAsync()
  {
    if (Interlocked.Exchange(ref _disposeCalled, 1) != 0)
      return; // Already disposed

    foreach (var server in _servers.Values) {
      await server.DisposeAsync();
    }
    _servers.Clear();

    foreach (var connection in _connections.Values) {
      await connection.DisposeAsync();
    }
    _connections.Clear();

    _eventChannel.Writer.Complete();
    await _eventChannel.Reader.Completion;
  }

  /// <summary>
  /// Gets the latest fact of type T for a session, or null if not available.
  /// </summary>
  public T? GetFact<T>(Guid sessionId) where T : Fact
  {
    if (_factCache.TryGetValue((sessionId, typeof(T)), out var entry))
      return entry.Fact as T;
    return null;
  }

  /// <summary>
  /// Gets the latest fact for a session and fact type, or null if not available.
  /// </summary>
  public Fact? GetFact(Guid sessionId, Type factType)
  {
    if (_factCache.TryGetValue((sessionId, factType), out var entry))
      return entry.Fact;
    return null;
  }

  /// <summary>
  /// Gets the latest fact and timestamp for a session and fact type, or null if not available.
  /// </summary>
  public (T? Fact, DateTimeOffset? LastUpdated) GetFactWithTimestamp<T>(Guid sessionId) where T : Fact
  {
    if (_factCache.TryGetValue((sessionId, typeof(T)), out var entry))
      return (entry.Fact as T, entry.LastUpdated);
    return (null, null);
  }

  /// <summary>
  /// Gets the latest fact and timestamp for a session and fact type, or null if not available.
  /// </summary>
  public (Fact? Fact, DateTimeOffset? LastUpdated) GetFactWithTimestamp(Guid sessionId, Type factType)
  {
    if (_factCache.TryGetValue((sessionId, factType), out var entry))
      return (entry.Fact, entry.LastUpdated);
    return (null, null);
  }
}
