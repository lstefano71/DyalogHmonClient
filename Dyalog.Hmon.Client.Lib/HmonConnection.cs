using Dyalog.Hmon.Client.Lib;

using Serilog;// Marker interface for payloads that support UID correlation
using Serilog.Core;

using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

/// <summary>
/// Represents a single HMON protocol connection, handling framing, handshake, and event dispatch.
/// </summary>
internal class HmonConnection : IAsyncDisposable
{
  private const string _supportedprotocol = "SupportedProtocols=2";
  private const string _usingprotocol = "UsingProtocol=2";
  private static readonly DrptFramer HmonFramer = new("HMON");

  private readonly ILogger _logger;
  private readonly TcpClient _tcpClient;

  private readonly Guid _sessionId;
  private readonly ChannelWriter<HmonEvent> _eventWriter;
  private readonly Func<Task>? _onDisconnect;
  private readonly Func<ClientConnectedEventArgs, Task>? _onClientConnected;
  private readonly CancellationTokenSource _cts = new();
  private readonly ConcurrentDictionary<string, TaskCompletionSource<HmonEvent>> _pendingRequests = new();
  private readonly TaskCompletionSource<bool> _pipeReadyTcs = new();

  /// <summary>
  /// Initializes a new HmonConnection for the given TCP client and session.
  /// </summary>
  /// <param name="tcpClient">Underlying TCP client.</param>
  /// <param name="sessionId">Session identifier.</param>
  /// <param name="eventWriter">Channel writer for events.</param>
  /// <param name="onDisconnect">Optional disconnect callback.</param>
  /// <param name="onClientConnected">Optional client connected callback.</param>
  public HmonConnection(TcpClient tcpClient, Guid sessionId, ChannelWriter<HmonEvent> eventWriter, Func<Task>? onDisconnect = null, Func<ClientConnectedEventArgs, Task>? onClientConnected = null)
  {
    _logger = Log.Logger.ForContext<HmonConnection>();
    _tcpClient = tcpClient;
    _sessionId = sessionId;
    _eventWriter = eventWriter;
    _onDisconnect = onDisconnect;
    _onClientConnected = onClientConnected;
  }

  /// <summary>
  /// Performs the HMON handshake and starts processing messages.
  /// </summary>
  /// <param name="ct">Cancellation token.</param>
  /// <param name="remoteAddress">Remote IP address for ClientConnected event.</param>
  /// <param name="remotePort">Remote port for ClientConnected event.</param>
  /// <param name="friendlyName">Optional friendly name for ClientConnected event.</param>
  public async Task InitializeAsync(CancellationToken ct, string remoteAddress, int remotePort, string? friendlyName = null)
  {
    try {
      _logger.Debug("Initializing HmonConnection (SessionId={SessionId})", _sessionId);

      _ = StartProcessingAsync(ct); // Start processing messages, including handshake responses
      await _pipeReadyTcs.Task; // Wait for pipe to be ready (handshake completed)

      if (_onClientConnected != null) {
        _logger.Debug("Firing ClientConnected event from HmonConnection (SessionId={SessionId})", _sessionId);
        await _onClientConnected.Invoke(new ClientConnectedEventArgs(_sessionId, remoteAddress, remotePort, friendlyName));
      }
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
    _logger.Warning("SendCommandAsync called with command: {Command}", command);
    string? uid = null;
    object actualPayload = payload;
    if (payload is IUidPayload uidPayload) {
      uid = Guid.NewGuid().ToString();
      uidPayload.UID = uid;
      actualPayload = payload;
    }

    var stream = _tcpClient.GetStream();
    var json = JsonSerializer.Serialize(new object[] { command, actualPayload });
    var bytes = Encoding.UTF8.GetBytes(json);

    _logger.Debug("SEND Message: {Json}", json);
    await HmonFramer.WriteFrameAsync(stream, bytes, ct);

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

  private async Task StartProcessingAsync(CancellationToken ct)
  {
    try {
      var pipe = new Pipe();
      Task reading = ReadPipeAsync(PipeReader.Create(_tcpClient.GetStream()), ct);
      await reading;
    } finally {
      if (_onDisconnect != null) {
        await _onDisconnect.Invoke();
      }
    }
  }

  private async Task ReadPipeAsync(PipeReader reader, CancellationToken ct)
  {
    var logger = Log.Logger.ForContext<HmonConnection>();
    try {
      // Handshake
      if (!await ProcessHandshakeAsync(reader, ct)) {
        throw new Exception($"Handshake failed for session: {_sessionId}.");
      }

      _pipeReadyTcs.TrySetResult(true); // Signal that the pipe is ready after handshake
      _logger.Debug("Handshake completed for session {SessionId}", _sessionId);

      while (!ct.IsCancellationRequested) {
        ReadResult result = await reader.ReadAsync(ct);
        ReadOnlySequence<byte> buffer = result.Buffer;

        while (true) // Loop to read all messages in the buffer
        {
          if (!TryReadMessage(ref buffer, out ReadOnlySequence<byte> message))
            break;
          // Normal message processing
          await ParseAndDispatchMessageAsync(message);
        }

        reader.AdvanceTo(buffer.Start, buffer.End);
        if (result.IsCompleted) break;
      }
    } catch (OperationCanceledException) {
      _logger.Debug("ReadPipeAsync canceled for session {SessionId}", _sessionId);
    } catch (Exception ex) {
      logger.Error(ex, "Exception in ReadPipeAsync for session {SessionId}", _sessionId);
    } finally {
      await reader.CompleteAsync();
    }
  }

  private async Task<bool> ProcessHandshakeAsync(PipeReader reader, CancellationToken ct)
  {
    var stream = _tcpClient.GetStream();
    await HmonFramer.WriteFrameAsync(stream, _supportedprotocol, ct);
    var msg = await ReadMessageAsStringAsync(reader, ct);
    if (msg != _supportedprotocol) {
      _logger.Error("Handshake failed: expected '{supportedprotocol}', got '{Message}'", _supportedprotocol, msg);
      return false; // Handshake failed
    }
    await HmonFramer.WriteFrameAsync(stream, _usingprotocol, ct);
    msg = await ReadMessageAsStringAsync(reader, ct);
    if (msg != _usingprotocol) {
      _logger.Error("Handshake failed: expected '{usingprotocol}', got '{Message}'", _usingprotocol, msg);
      return false; // Handshake failed
    }
    return true; // Handshake successful
  }

  private async Task<string> ReadMessageAsStringAsync(PipeReader reader, CancellationToken ct)
  {
    try {
      while (true) {
        ReadResult result = await reader.ReadAsync(ct);
        ReadOnlySequence<byte> buffer = result.Buffer;
        if (TryReadMessage(ref buffer, out var message)) {
          var messageBytes = Encoding.UTF8.GetString(message.ToArray());
          reader.AdvanceTo(buffer.Start);
          return messageBytes;
        }
        if (result.IsCompleted && buffer.IsEmpty) {
          throw new EndOfStreamException("No more data available in the stream.");
        }
      }
    } catch (OperationCanceledException) {
      _logger.Debug("ReadMessageAsStringAsync canceled for session {SessionId}", _sessionId);
      throw;
    } catch (Exception ex) {
      _logger.Error(ex, "Error reading message as string for session {SessionId}", _sessionId);
      throw;
    }
  }

  private bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> message)
  {
    message = default;
    if (buffer.Length < 4) return false; // Not enough for length prefix

    var lengthBytes = buffer.Slice(0, 4).ToArray();
    if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
    var messageLength = BitConverter.ToInt32(lengthBytes, 0);

    int headerOffset = 4; // For length prefix

    if (buffer.Length < 8) return false; // Not enough for length + magic
    var magicBytes = buffer.Slice(4, 4).ToArray();
    // Validate magic number using the framer's magic
    if (!magicBytes.AsSpan().SequenceEqual(HmonFramer.Magic)) {
      throw new InvalidOperationException($"Invalid handshake magic number encountered. Expected '{Encoding.ASCII.GetString(HmonFramer.Magic)}', got '{Encoding.ASCII.GetString(magicBytes)}'");
    }
    headerOffset = 8; // For length + magic

    // FIX: Only require messageLength bytes (total frame size) in buffer
    if (buffer.Length < messageLength) return false; // Not enough for full message

    message = buffer.Slice(headerOffset, messageLength - headerOffset); // Adjust slice for magic number
    buffer = buffer.Slice(message.End);

    _logger.Debug("{direction} Message: {Raw}", "RECV", Encoding.UTF8.GetString(message.ToArray()));
    return true;
  }

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

    HmonEvent? hmonEvent = command switch {
      "Facts" => new FactsReceivedEvent(_sessionId, JsonSerializer.Deserialize<FactsResponse>(ref reader, HmonJsonContext.Default.FactsResponse)!),
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

  private string? GetUid(HmonEvent hmonEvent)
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
    await _eventWriter.WaitToWriteAsync();
  }
}