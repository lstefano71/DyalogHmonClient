using Dyalog.Hmon.Client.Lib.Exceptions;

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
  /// Unified asynchronous event stream for all HMON events, including lifecycle events.
  /// </summary>
  public IAsyncEnumerable<HmonEvent> Events => WatchAndCacheFacts();
  // Internal: watches event stream and updates fact cache
  private async IAsyncEnumerable<HmonEvent> WatchAndCacheFacts([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
  {
    await foreach (var evt in _eventChannel.Reader.ReadAllAsync(ct))
    {
        if (evt is FactsReceivedEvent factsEvt)
        {
            foreach (var fact in factsEvt.Facts.Facts)
            {
                if (fact != null)
                {
                    _factCache[(evt.SessionId, fact.GetType())] = new FactCacheEntry(fact, DateTimeOffset.UtcNow);
                }
            }
        }
        yield return evt;
    }
    Log.Information("WatchAndCacheFacts: Exiting after cancellation or completion.");
  }
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
                async (reason) => {
                  logger.Debug("Connection closed (SessionId={SessionId})", sessionId);
                  _connections.TryRemove(sessionId, out _);
                  await _eventChannel.Writer.WriteAsync(new SessionDisconnectedEvent(sessionId, remoteEndPoint.Address.ToString(), remoteEndPoint.Port, null, reason));
                }
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
          } catch (OperationCanceledException) {
            logger.Information("Listener accept canceled due to shutdown.");
            break;
          } catch (Exception ex) {
            logger.Error(ex, "Listener loop error");
            OnError?.Invoke(ex, null);
            continue;
          }
        }
      } catch (OperationCanceledException) {
        logger.Information("Listener stopped due to cancellation.");
        // Optionally: do not log as error
      } catch (Exception ex) {
        logger.Error(ex, "Listener loop error");
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
  /// A private helper to send a command, await a specific event response, and handle timeouts and exceptions.
  /// </summary>
  /// <typeparam name="TResponseEvent">The expected HmonEvent type in response to the command.</typeparam>
  /// <param name="sessionId">The ID of the target session.</param>
  /// <param name="commandName">The name of the HMON command to send.</param>
  /// <param name="payload">The payload object for the command.</param>
  /// <param name="timeoutOverride">An optional timeout to override the default.</param>
  /// <param name="userCt">The user-provided cancellation token.</param>
  /// <returns>The received event of the specified type.</returns>
  /// <exception cref="SessionNotFoundException">Thrown if the sessionId is not valid or connected.</exception>
  /// <exception cref="CommandTimeoutException">Thrown if the command does not receive a response within the effective timeout.</exception>
  /// <exception cref="HmonConnectionException">Thrown for other underlying connection or protocol errors.</exception>
  private async Task<TResponseEvent> SendCommandAndAwaitResponseAsync<TResponseEvent>(
      Guid sessionId,
      string commandName,
      object payload,
      TimeSpan? timeoutOverride,
      CancellationToken userCt) where TResponseEvent : HmonEvent
  {
    if (!_connections.TryGetValue(sessionId, out var conn)) {
      OnError?.Invoke(new SessionNotFoundException(sessionId), sessionId);
      throw new SessionNotFoundException(sessionId);
    }
    var effectiveTimeout = timeoutOverride ?? _options.DefaultCommandTimeout;
    using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(userCt, timeoutCts.Token);
    try {
      return await conn.SendCommandAsync<TResponseEvent>(commandName, payload, linkedCts.Token);
    } catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !userCt.IsCancellationRequested) {
      throw new CommandTimeoutException(commandName, effectiveTimeout);
    }
  }
  /// <summary>
  /// Requests a one-time, guaranteed-fresh snapshot of facts from the interpreter.
  /// This method will always perform a network request.
  /// </summary>
  /// <param name="sessionId">The ID of the target session.</param>
  /// <param name="facts">An enumeration of the facts to retrieve.</param>
  /// <param name="timeout">An optional timeout for this specific command. If null, the default timeout will be used.</param>
  /// <param name="ct">A cancellation token for the operation.</param>
  /// <returns>A task that represents the asynchronous operation. The task result contains the requested facts.</returns>
  /// <exception cref="SessionNotFoundException">Thrown if the specified sessionId does not match an active, connected session.</exception>
  /// <exception cref="CommandTimeoutException">Thrown if the interpreter does not respond within the effective timeout.</exception>
  /// <exception cref="HmonConnectionException">Thrown for underlying connection or protocol errors during the command.</exception>
  public async Task<FactsResponse> GetFactsAsync(
      Guid sessionId,
      IEnumerable<FactType> facts,
      TimeSpan? timeout = null,
      CancellationToken ct = default)
  {
    var payload = new GetFactsPayload([.. facts.Select(f => (int)f)]);
    var factsReceivedEvent = await SendCommandAndAwaitResponseAsync<FactsReceivedEvent>(
        sessionId,
        "GetFacts",
        payload,
        timeout,
        ct
    );
    return factsReceivedEvent.Facts;
  }

  /// <summary>
  /// Requests a high-priority status report from the interpreter.
  /// </summary>
  /// <param name="sessionId">Session ID.</param>
  /// <param name="timeout">Optional timeout for this command.</param>
  /// <param name="ct">Cancellation token.</param>
  /// <exception cref="SessionNotFoundException">Thrown if the specified sessionId does not match an active, connected session.</exception>
  public async Task<LastKnownStateResponse> GetLastKnownStateAsync(Guid sessionId, TimeSpan? timeout = null, CancellationToken ct = default)
  {
    var payload = new LastKnownStatePayload();
    var evt = await SendCommandAndAwaitResponseAsync<LastKnownStateReceivedEvent>(sessionId, "GetLastKnownState", payload, timeout, ct);
    return evt.State;
  }
  /// <summary>
  /// Starts polling facts from the interpreter at a given interval.
  /// </summary>
  /// <param name="sessionId">Session ID.</param>
  /// <param name="facts">Facts to poll.</param>
  /// <param name="interval">Polling interval.</param>
  /// <param name="timeout">Optional timeout for this command.</param>
  /// <param name="ct">Cancellation token.</param>
  /// <exception cref="SessionNotFoundException">Thrown if the specified sessionId does not match an active, connected session.</exception>
  public async Task<FactsReceivedEvent> PollFactsAsync(Guid sessionId, IEnumerable<FactType> facts, TimeSpan interval, TimeSpan? timeout = null, CancellationToken ct = default)
  {
    var payload = new PollFactsPayload([.. facts.Select(f => (int)f)], (int)interval.TotalMilliseconds);
    return await SendCommandAndAwaitResponseAsync<FactsReceivedEvent>(sessionId, "PollFacts", payload, timeout, ct);
  }
  /// <summary>
  /// Stops any active facts polling for the given session.
  /// </summary>
  /// <param name="sessionId">Session ID.</param>
  /// <param name="timeout">Optional timeout for this command.</param>
  /// <param name="ct">Cancellation token.</param>
  /// <exception cref="SessionNotFoundException">Thrown if the specified sessionId does not match an active, connected session.</exception>
  public async Task StopFactsPollingAsync(Guid sessionId, TimeSpan? timeout = null, CancellationToken ct = default)
  {
    var payload = new { };
    await SendCommandAndAwaitResponseAsync<FactsReceivedEvent>(sessionId, "StopFacts", payload, timeout, ct);
  }
  /// <summary>
  /// Triggers an immediate facts message from an active poll.
  /// </summary>
  /// <param name="sessionId">Session ID.</param>
  /// <param name="timeout">Optional timeout for this command.</param>
  /// <param name="ct">Cancellation token.</param>
  /// <exception cref="SessionNotFoundException">Thrown if the specified sessionId does not match an active, connected session.</exception>
  public async Task BumpFactsAsync(Guid sessionId, TimeSpan? timeout = null, CancellationToken ct = default)
  {
    var payload = new { };
    await SendCommandAndAwaitResponseAsync<FactsReceivedEvent>(sessionId, "BumpFacts", payload, timeout, ct);
  }
  /// <summary>
  /// Subscribes to interpreter events for the given session.
  /// </summary>
  /// <param name="sessionId">Session ID.</param>
  /// <param name="events">Events to subscribe to.</param>
  /// <param name="timeout">Optional timeout for this command.</param>
  /// <param name="ct">Cancellation token.</param>
  /// <exception cref="SessionNotFoundException">Thrown if the specified sessionId does not match an active, connected session.</exception>
  public async Task<SubscribedResponseReceivedEvent> SubscribeAsync(Guid sessionId, IEnumerable<SubscriptionEvent> events, TimeSpan? timeout = null, CancellationToken ct = default)
  {
    var payload = new SubscribePayload([.. events.Select(e => (int)e)]);
    return await SendCommandAndAwaitResponseAsync<SubscribedResponseReceivedEvent>(sessionId, "Subscribe", payload, timeout, ct);
  }
  /// <summary>
  /// Requests the interpreter to connect to a RIDE client.
  /// </summary>
  /// <param name="sessionId">Session ID.</param>
  /// <param name="address">RIDE address.</param>
  /// <param name="port">RIDE port.</param>
  /// <param name="timeout">Optional timeout for this command.</param>
  /// <param name="ct">Cancellation token.</param>
  /// <exception cref="SessionNotFoundException">Thrown if the specified sessionId does not match an active, connected session.</exception>
  public async Task<RideConnectionResponse> ConnectRideAsync(Guid sessionId, string address, int port, TimeSpan? timeout = null, CancellationToken ct = default)
  {
    var payload = new {
      Address = address,
      Port = port,
      UID = Guid.NewGuid().ToString()
    };
    var evt = await SendCommandAndAwaitResponseAsync<RideConnectionReceivedEvent>(sessionId, "ConnectRide", payload, timeout, ct);
    return evt.Response;
  }
  /// <summary>
  /// Requests the interpreter to disconnect from any RIDE client.
  /// </summary>
  /// <param name="sessionId">Session ID.</param>
  /// <param name="timeout">Optional timeout for this command.</param>
  /// <param name="ct">Cancellation token.</param>
  /// <exception cref="SessionNotFoundException">Thrown if the specified sessionId does not match an active, connected session.</exception>
  public async Task<RideConnectionResponse> DisconnectRideAsync(Guid sessionId, TimeSpan? timeout = null, CancellationToken ct = default)
  {
    var payload = new { };
    var evt = await SendCommandAndAwaitResponseAsync<RideConnectionReceivedEvent>(sessionId, "ConnectRide", payload, timeout, ct);
    return evt.Response;
  }
  /// <summary>
  /// Disposes all managed connections and resources.
  /// </summary>
  public async ValueTask DisposeAsync()
  {
    Log.Information("Orchestrator: Starting disposal...");
    if (Interlocked.Exchange(ref _disposeCalled, 1) != 0)
    {
        Log.Information("Orchestrator: Already disposed.");
        return;
    }
    Log.Information("Orchestrator: Disposing servers...");
    foreach (var server in _servers.Values)
    {
        Log.Information($"Orchestrator: Disposing server {server}");
        await server.DisposeAsync();
    }
    Log.Information("Orchestrator: Servers disposed.");
    _servers.Clear();

    Log.Information("Orchestrator: Disposing connections...");
    foreach (var connection in _connections.Values)
    {
        Log.Information($"Orchestrator: Disposing connection {connection}");
        await connection.DisposeAsync();
    }
    Log.Information("Orchestrator: Connections disposed.");
    _connections.Clear();

    Log.Information("Orchestrator: Completing event channel...");
    _eventChannel.Writer.Complete();
    Log.Information("Orchestrator: Awaiting channel completion...");
    var completionTask = _eventChannel.Reader.Completion;
    if (await Task.WhenAny(completionTask, Task.Delay(TimeSpan.FromSeconds(10))) != completionTask)
    {
        Log.Warning("Orchestrator: Channel completion timed out.");
    }
    else
    {
        Log.Information("Orchestrator: Disposal complete.");
    }
  }
  /// <summary>
  /// Gets the latest fact of type T for a session.
  /// Returns null if the fact is not in the cache or if the cached fact is older than the configured FactCacheTTL.
  /// </summary>
  public T? GetFact<T>(Guid sessionId) where T : Fact
  {
    if (_factCache.TryGetValue((sessionId, typeof(T)), out var entry)) {
      if (DateTimeOffset.UtcNow - entry.LastUpdated > _options.FactCacheTTL) {
        _factCache.TryRemove((sessionId, typeof(T)), out _);
        return null;
      }
      return entry.Fact as T;
    }
    return null;
  }
  /// <summary>
  /// Gets the latest fact for a session and fact type, or null if not available.
  /// </summary>
  public Fact? GetFact(Guid sessionId, Type factType)
  {
    return _factCache.TryGetValue((sessionId, factType), out var entry) ? entry.Fact : null;
  }
  /// <summary>
  /// Gets the latest fact and its update timestamp.
  /// Returns (null, null) if the fact is not in the cache or if the cached fact is older than the configured FactCacheTTL.
  /// </summary>
  public (T? Fact, DateTimeOffset? LastUpdated) GetFactWithTimestamp<T>(Guid sessionId) where T : Fact
  {
    if (_factCache.TryGetValue((sessionId, typeof(T)), out var entry)) {
      if (DateTimeOffset.UtcNow - entry.LastUpdated > _options.FactCacheTTL) {
        _factCache.TryRemove((sessionId, typeof(T)), out _);
        return (null, null);
      }
      return (entry.Fact as T, entry.LastUpdated);
    }
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
