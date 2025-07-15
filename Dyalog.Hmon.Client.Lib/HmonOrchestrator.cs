using Serilog;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using System.Threading.Tasks;

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

  private int _disposeCalled = 0;

  /// <summary>
  /// Unified asynchronous event stream for all HMON events.
  /// </summary>
  public IAsyncEnumerable<HmonEvent> Events => _eventChannel.Reader.ReadAllAsync();

  /// <summary>
  /// Event fired when a client connects.
  /// </summary>
  public event Func<ClientConnectedEventArgs, Task>? ClientConnected;
  /// <summary>
  /// Event fired when a client disconnects.
  /// </summary>
  public event Func<ClientDisconnectedEventArgs, Task>? ClientDisconnected;

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
      Serilog.Log.Logger.ForContext<HmonOrchestrator>().Error("GetFactsAsync: Session not found for sessionId={SessionId}", sessionId);
      throw new InvalidOperationException("Session not found");
    }
    var payload = new GetFactsPayload([.. facts.Select(f => (int)f)]);
    var evt = await conn.SendCommandAsync<FactsReceivedEvent>("GetFacts", payload, ct);
    return evt.Facts;
  }

  /// <summary>
  /// Requests a high-priority status report from the interpreter.
  /// </summary>
  /// <param name="sessionId">Session ID.</param>
  /// <param name="ct">Cancellation token.</param>
  public async Task<LastKnownStateResponse> GetLastKnownStateAsync(Guid sessionId, CancellationToken ct = default)
  {
    if (!_connections.TryGetValue(sessionId, out var conn))
      throw new InvalidOperationException("Session not found");
    var payload = new LastKnownStatePayload();
    var evt = await conn.SendCommandAsync<LastKnownStateReceivedEvent>("GetLastKnownState", payload, ct);
    return evt.State;
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
    if (!_connections.TryGetValue(sessionId, out var conn))
      throw new InvalidOperationException("Session not found");
    var payload = new PollFactsPayload([.. facts.Select(f => (int)f)], (int)interval.TotalMilliseconds);
    await conn.SendCommandAsync<FactsReceivedEvent>("PollFacts", payload, ct);
  }

  /// <summary>
  /// Stops any active facts polling for the given session.
  /// </summary>
  /// <param name="sessionId">Session ID.</param>
  /// <param name="ct">Cancellation token.</param>
  public async Task StopFactsPollingAsync(Guid sessionId, CancellationToken ct = default)
  {
    if (!_connections.TryGetValue(sessionId, out var conn))
      throw new InvalidOperationException("Session not found");
    var payload = new { };
    await conn.SendCommandAsync<FactsReceivedEvent>("StopFacts", payload, ct);
  }

  /// <summary>
  /// Triggers an immediate facts message from an active poll.
  /// </summary>
  /// <param name="sessionId">Session ID.</param>
  /// <param name="ct">Cancellation token.</param>
  public async Task BumpFactsAsync(Guid sessionId, CancellationToken ct = default)
  {
    if (!_connections.TryGetValue(sessionId, out var conn))
      throw new InvalidOperationException("Session not found");
    var payload = new { };
    await conn.SendCommandAsync<FactsReceivedEvent>("BumpFacts", payload, ct);
  }

  /// <summary>
  /// Subscribes to interpreter events for the given session.
  /// </summary>
  /// <param name="sessionId">Session ID.</param>
  /// <param name="events">Events to subscribe to.</param>
  /// <param name="ct">Cancellation token.</param>
  public async Task SubscribeAsync(Guid sessionId, IEnumerable<SubscriptionEvent> events, CancellationToken ct = default)
  {
    if (!_connections.TryGetValue(sessionId, out var conn))
      throw new InvalidOperationException("Session not found");
    var payload = new SubscribePayload([.. events.Select(e => (int)e)]);
    await conn.SendCommandAsync<SubscribedResponseReceivedEvent>("Subscribe", payload, ct);
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
    if (!_connections.TryGetValue(sessionId, out var conn))
      throw new InvalidOperationException("Session not found");
    var payload = new {
      Address = address,
      Port = port,
      UID = Guid.NewGuid().ToString()
    };
    var evt = await conn.SendCommandAsync<RideConnectionReceivedEvent>("ConnectRide", payload, ct);
    return evt.Response;
  }

  /// <summary>
  /// Requests the interpreter to disconnect from any RIDE client.
  /// </summary>
  /// <param name="sessionId">Session ID.</param>
  /// <param name="ct">Cancellation token.</param>
  public async Task<RideConnectionResponse> DisconnectRideAsync(Guid sessionId, CancellationToken ct = default)
  {
    if (!_connections.TryGetValue(sessionId, out var conn))
      throw new InvalidOperationException("Session not found");
    var payload = new { };
    var evt = await conn.SendCommandAsync<RideConnectionReceivedEvent>("ConnectRide", payload, ct);
    return evt.Response;
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
}
