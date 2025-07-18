using Dyalog.Hmon.Client.Lib;
using Dyalog.Hmon.Client.Lib.Exceptions;

using Serilog;

using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
/// <summary>
/// Represents a single HMON protocol connection, handling only HMON-specific parsing and dispatch.
/// </summary>
internal class HmonConnection : IAsyncDisposable
{
  private readonly DrptFramer _hmonFramer;
  private readonly ILogger _logger;
  private readonly TcpClient _tcpClient;
  private readonly Guid _sessionId;
  private readonly ChannelWriter<HmonEvent> _eventWriter;
  private readonly Func<string, Task>? _onDisconnect;
  private readonly CancellationTokenSource _cts = new();
  private readonly ConcurrentDictionary<string, TaskCompletionSource<HmonEvent>> _pendingRequests = new();
  private readonly TaskCompletionSource<bool> _pipeReadyTcs = new();
  private Task? _processingTask; // Track the background processing task

  /// <summary>
  /// A task that completes when the connection's processing loop terminates.
  /// </summary>
  public Task Completion => _processingTask;


  /// <summary>
  /// Initializes a new HmonConnection for the given TCP client and session.
  /// </summary>
  /// <param name="tcpClient">Underlying TCP client.</param>
  /// <param name="sessionId">Session identifier.</param>
  /// <param name="eventWriter">Channel writer for events.</param>
  /// <param name="onDisconnect">Optional disconnect callback.</param>
  public HmonConnection(TcpClient tcpClient, Guid sessionId, ChannelWriter<HmonEvent> eventWriter, Func<string, Task>? onDisconnect = null)
  {
    _logger = Log.Logger.ForContext<HmonConnection>();
    _tcpClient = tcpClient;
    _sessionId = sessionId;
    _eventWriter = eventWriter;
    _onDisconnect = onDisconnect;
    _hmonFramer = new DrptFramer("HMON", _tcpClient.GetStream());
  }
  /// <summary>
  /// Performs the HMON handshake and starts processing messages.
  /// </summary>
  /// <param name="remoteAddress">Remote IP address for ClientConnected event.</param>
  /// <param name="remotePort">Remote port for ClientConnected event.</param>
  /// <param name="ct">Cancellation token.</param>
  /// <param name="friendlyName">Optional friendly name for ClientConnected event.</param>
  public async Task InitializeAsync(string remoteAddress, int remotePort, string? friendlyName = null, CancellationToken ct = default)
  {
    try {
      _logger.Debug("Initializing HmonConnection (SessionId={SessionId})", _sessionId);
      _processingTask = StartProcessingAsync(remoteAddress, remotePort, friendlyName, ct); // Pass details for disconnect event
      await _pipeReadyTcs.Task; // Wait for pipe to be ready (handshake completed)

      _logger.Debug("Firing SessionConnectedEvent from HmonConnection (SessionId={SessionId})", _sessionId);
      await _eventWriter.WriteAsync(new SessionConnectedEvent(_sessionId, remoteAddress, remotePort, friendlyName), ct);
    } catch (Exception ex) {
      _logger.Error(ex, "HmonConnection initialization failed (SessionId={SessionId})", _sessionId);
      throw;
    }
  }
  /// <summary>
  /// Sends a command to the interpreter and awaits a strongly-typed event response.
  /// </summary>
  /// <typeparam name="T">Expected event type.</typeparam>
  /// <param name="command">Command name.</param>
  /// <param name="payload">Command payload.</param>
  /// <param name="ct">Cancellation token.</param>
  public async Task<T> SendCommandAsync<T>(string command, object payload, CancellationToken ct) where T : HmonEvent
  {
    string? uid = null;
    object actualPayload = payload;
    if (payload is IUidPayload uidPayload) {
      uid = Guid.NewGuid().ToString();
      uidPayload.UID = uid;
      actualPayload = payload;
    }

    var json = JsonSerializer.Serialize(new object[] { command, actualPayload });
    var bytes = Encoding.UTF8.GetBytes(json);
    _logger.Debug("SEND Message: {Json}", json);
    await _hmonFramer.WriteFrameAsync(bytes, ct);
    if (uid != null) {
      var tcs = new TaskCompletionSource<HmonEvent>();
      _pendingRequests.TryAdd(uid, tcs);
      using (ct.Register(() => tcs.TrySetCanceled())) {
        var result = await tcs.Task;
        return (T)result;
      }
    } else {
      // No UID, fire-and-forget, return default
      return default!;
    }
  }
/// <summary>
/// Main processing loop for HMON connection: handshake, message reading, and event dispatch.
/// </summary>
/// <param name="host">Remote host address.</param>
/// <param name="port">Remote port.</param>
/// <param name="friendlyName">Optional friendly name.</param>
/// <param name="ct">Cancellation token.</param>
private async Task StartProcessingAsync(string host, int port, string? friendlyName, CancellationToken ct)
{
  string reason = "Unknown";
  try {
    var pipeReader = PipeReader.Create(_tcpClient.GetStream());
    // Perform handshake using DrptFramer
    bool handshakeOk = await _hmonFramer.PerformHandshakeAsync(ct);
    if (!handshakeOk) {
      throw new HmonHandshakeFailedException($"Handshake failed for session: {_sessionId}.", null);
    }
    _pipeReadyTcs.TrySetResult(true);
    _logger.Debug("Handshake completed for session {SessionId}", _sessionId);
    // Main message loop
    while (!ct.IsCancellationRequested) {
      var message = await _hmonFramer.ReadNextMessageAsync(ParseAndDispatchMessageAsync, ct);
    }
    reason = "Cancellation requested";
  } catch (OperationCanceledException) {
    reason = "Operation canceled";
    _logger.Debug("StartProcessingAsync canceled for session {SessionId}", _sessionId);
  } catch (Exception ex) {
    reason = ex.Message;
    _logger.Error(ex, "Exception in StartProcessingAsync for session {SessionId}", _sessionId);
  } finally {
    if (_onDisconnect != null) {
      await _onDisconnect.Invoke(reason);
    } else {
      await _eventWriter.WriteAsync(new SessionDisconnectedEvent(_sessionId, host, port, friendlyName, reason), ct);
    }
  }
}
  // Add a static field to cache JsonSerializerOptions
  private static readonly JsonSerializerOptions CachedJsonSerializerOptions = new() {
    Converters = { new FactJsonConverter() }
  };
  // Update the code to use the cached JsonSerializerOptions instance
/// <summary>
/// Parses a protocol message and dispatches the corresponding HMON event.
/// </summary>
/// <param name="message">Protocol message buffer.</param>
private async Task ParseAndDispatchMessageAsync(ReadOnlySequence<byte> message)
{
  var reader = new Utf8JsonReader(message);
  if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
    return;
  if (!reader.Read() || reader.TokenType != JsonTokenType.String)
    return;
  var command = reader.GetString();
  if (!reader.Read())
    return;
  HmonEvent? hmonEvent =
      command switch {
        "Facts" => new FactsReceivedEvent(
              _sessionId,
              JsonSerializer.Deserialize<FactsResponse>(
                  ref reader,
                  CachedJsonSerializerOptions // Use cached options here
              )!
          ),
        "Notification" => new NotificationReceivedEvent(_sessionId, JsonSerializer.Deserialize<NotificationResponse>(ref reader, HmonJsonContext.Default.NotificationResponse)!),
        "LastKnownState" => new LastKnownStateReceivedEvent(_sessionId, JsonSerializer.Deserialize<LastKnownStateResponse>(ref reader, HmonJsonContext.Default.LastKnownStateResponse)!),
        "Subscribed" => new SubscribedResponseReceivedEvent(_sessionId, JsonSerializer.Deserialize<SubscribedResponse>(ref reader, HmonJsonContext.Default.SubscribedResponse)!),
        "RideConnection" => new RideConnectionReceivedEvent(_sessionId, JsonSerializer.Deserialize<RideConnectionResponse>(ref reader, HmonJsonContext.Default.RideConnectionResponse)!),
        "UserMessage" => new UserMessageReceivedEvent(_sessionId, JsonSerializer.Deserialize<UserMessageResponse>(ref reader, HmonJsonContext.Default.UserMessageResponse)!),
        "UnknownCommand" => new UnknownCommandEvent(_sessionId, JsonSerializer.Deserialize<UnknownCommandResponse>(ref reader, HmonJsonContext.Default.UnknownCommandResponse)!),
        "MalformedCommand" => new MalformedCommandEvent(_sessionId, JsonSerializer.Deserialize<MalformedCommandResponse>(ref reader, HmonJsonContext.Default.MalformedCommandResponse)!),
        "InvalidSyntax" => new InvalidSyntaxEvent(_sessionId, JsonSerializer.Deserialize<InvalidSyntaxResponse>(ref reader, HmonJsonContext.Default.InvalidSyntaxResponse)!),
        "DisallowedUID" => new DisallowedUidEvent(_sessionId, JsonSerializer.Deserialize<DisallowedUidResponse>(ref reader, HmonJsonContext.Default.DisallowedUidResponse)!),
        _ => null
      };
  if (hmonEvent != null) {
    var uid = GetUid(hmonEvent);
    if (uid != null && _pendingRequests.TryRemove(uid, out var tcs)) {
      tcs.TrySetResult(hmonEvent);
    } else {
      await _eventWriter.WriteAsync(hmonEvent);
    }
  }
}
/// <summary>
/// Extracts the UID from a HMON event, if present.
/// </summary>
/// <param name="hmonEvent">HMON event instance.</param>
/// <returns>UID string or null.</returns>
private static string? GetUid(HmonEvent hmonEvent)
{
  return hmonEvent switch {
    FactsReceivedEvent e => e.Facts.UID,
    NotificationReceivedEvent e => e.Notification.UID,
    LastKnownStateReceivedEvent e => e.State.UID,
    SubscribedResponseReceivedEvent e => e.Response.UID,
    RideConnectionReceivedEvent e => e.Response.UID,
    UserMessageReceivedEvent e => e.Message.UID,
    UnknownCommandEvent e => e.Error.UID,
    MalformedCommandEvent e => e.Error.UID,
    DisallowedUidEvent e => e.Error.UID,
    _ => null
  };
}
  /// <summary>
  /// Disposes the connection and releases resources.
  /// </summary>
  public async ValueTask DisposeAsync()
  {
    _cts.Cancel();
    _tcpClient.Close();
    if (_processingTask != null) {
      try {
        await _processingTask;
      } catch (OperationCanceledException) {
        // Expected on shutdown
      }
    }
    await _eventWriter.WaitToWriteAsync();
  }
}
