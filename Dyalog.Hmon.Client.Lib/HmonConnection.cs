using Dyalog.Hmon.Client.Lib;

using Serilog;// Marker interface for payloads that support UID correlation

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

  private readonly TcpClient _tcpClient;
  private static readonly DrptFramer HmonFramer = new("HMON");

  private readonly Guid _sessionId;
  private readonly ChannelWriter<HmonEvent> _eventWriter;
  private readonly Func<Task>? _onDisconnect;
  private readonly Func<ClientConnectedEventArgs, Task>? _onClientConnected;
  private readonly CancellationTokenSource _cts = new();
  private readonly ConcurrentDictionary<string, TaskCompletionSource<HmonEvent>> _pendingRequests = new();
  private readonly TaskCompletionSource<bool> _pipeReadyTcs = new();
  private readonly Queue<string> _handshakeExpectedPayloads = new Queue<string>();

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
    var logger = Log.Logger.ForContext<HmonConnection>();
    try
    {
      logger.Debug("Initializing HmonConnection (SessionId={SessionId})", _sessionId);

      // Send initial handshake messages
      var stream = _tcpClient.GetStream();
      await HmonFramer.WriteFrameAsync(stream, Encoding.UTF8.GetBytes("SupportedProtocols=2"), ct);
      logger.Debug("SENT Handshake: SupportedProtocols=2");
      await HmonFramer.WriteFrameAsync(stream, Encoding.UTF8.GetBytes("UsingProtocol=2"), ct);
      logger.Debug("SENT Handshake: UsingProtocol=2");

      // Populate expected handshake responses
      _handshakeExpectedPayloads.Enqueue("SupportedProtocols=2");
      _handshakeExpectedPayloads.Enqueue("UsingProtocol=2");

      _ = StartProcessingAsync(ct); // Start processing messages, including handshake responses
      await _pipeReadyTcs.Task; // Wait for pipe to be ready (handshake completed)

      if (_onClientConnected != null)
      {
        logger.Debug("Firing ClientConnected event from HmonConnection (SessionId={SessionId})", _sessionId);
        await _onClientConnected.Invoke(new ClientConnectedEventArgs(_sessionId, remoteAddress, remotePort, friendlyName));
      }
    }
    catch (Exception ex)
    {
      logger.Error(ex, "HmonConnection initialization failed (SessionId={SessionId})", _sessionId);
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
    if (payload is IUidPayload uidPayload)
    {
      uid = Guid.NewGuid().ToString();
      uidPayload.UID = uid;
      actualPayload = payload;
    }

    var stream = _tcpClient.GetStream();
    var json = JsonSerializer.Serialize(new object[] { command, actualPayload });
    var bytes = Encoding.UTF8.GetBytes(json);

    Log.Debug("SEND Message: {Json}", json);
    await HmonFramer.WriteFrameAsync(stream, bytes, ct);

    if (uid != null)
    {
      var tcs = new TaskCompletionSource<HmonEvent>();
      _pendingRequests.TryAdd(uid, tcs);
      using (ct.Register(() => tcs.TrySetCanceled()))
      {
        var result = await tcs.Task;
        return (T)result;
      }
    }
    else
    {
      // No UID, fire-and-forget, return default
      return default!;
    }
  }

  private async Task StartProcessingAsync(CancellationToken ct)
  {
    try
    {
      var pipe = new Pipe();
      Task writing = FillPipeAsync(_tcpClient.GetStream(), pipe.Writer, ct);
      Task reading = ReadPipeAsync(pipe.Reader, ct);

      // _pipeReadyTcs.TrySetResult(); // Signal that the pipe is ready - now done after handshake

      await Task.WhenAll(reading, writing);
    }
    finally
    {
      if (_onDisconnect != null)
      {
        await _onDisconnect.Invoke();
      }
    }
  }

  private async Task FillPipeAsync(NetworkStream stream, PipeWriter writer, CancellationToken ct)
  {
    const int minimumBufferSize = 512;
    var logger = Log.Logger.ForContext<HmonConnection>();
    while (!ct.IsCancellationRequested)
    {
      Memory<byte> memory = writer.GetMemory(minimumBufferSize);
      try
      {
        int bytesRead = await stream.ReadAsync(memory, ct);
        if (bytesRead == 0) break;
        writer.Advance(bytesRead);
      }
      catch (OperationCanceledException)
      {
        logger.Debug("FillPipeAsync canceled for session {SessionId}", _sessionId);
        break;
      }
      catch (Exception ex)
      {
        logger.Error(ex, "Exception in FillPipeAsync for session {SessionId}", _sessionId);
        break;
      }

      FlushResult result = await writer.FlushAsync(ct);
      if (result.IsCompleted) break;
    }
    await writer.CompleteAsync();
  }

  private async Task ReadPipeAsync(PipeReader reader, CancellationToken ct)
  {
    var logger = Log.Logger.ForContext<HmonConnection>();
    try
    {
      while (!ct.IsCancellationRequested)
      {
        ReadResult result = await reader.ReadAsync(ct);
        ReadOnlySequence<byte> buffer = result.Buffer;

        while (TryReadMessage(ref buffer, out var message))
        {
          if (_handshakeExpectedPayloads.Count > 0)
          {
            // Process handshake messages
            var receivedPayload = Encoding.UTF8.GetString(message.ToArray());
            logger.Debug("RECV Handshake Message: {Payload}", receivedPayload);

            var expectedPayload = _handshakeExpectedPayloads.Dequeue();
            if (receivedPayload != expectedPayload)
            {
              throw new InvalidOperationException($"Handshake payload mismatch. Expected '{expectedPayload}', got '{receivedPayload}'");
            }

            if (_handshakeExpectedPayloads.Count == 0)
            {
              _pipeReadyTcs.TrySetResult(true); // Handshake completed, signal pipe is ready
              logger.Debug("Handshake completed for session {SessionId}", _sessionId);
            }
          }
          else
          {
            // Normal message processing
            logger.Debug("RECV Message: {Raw}", Encoding.UTF8.GetString(message.ToArray()));
            await ParseAndDispatchMessageAsync(message);
          }
        }

        reader.AdvanceTo(buffer.Start, buffer.End);
        if (result.IsCompleted) break;
      }
    }
    catch (OperationCanceledException)
    {
      logger.Debug("ReadPipeAsync canceled for session {SessionId}", _sessionId);
    }
    catch (Exception ex)
    {
      logger.Error(ex, "Exception in ReadPipeAsync for session {SessionId}", _sessionId);
    }
    finally
    {
      await reader.CompleteAsync();
    }
  }

  private bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> message)
  {
    if (buffer.Length < 4)
    {
      message = default;
      return false;
    }

    var lengthBytes = buffer.Slice(0, 4).ToArray();
    if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
    var messageLength = BitConverter.ToInt32(lengthBytes, 0);

    if (buffer.Length < messageLength + 4)
    {
      message = default;
      return false;
    }

    message = buffer.Slice(4, messageLength);
    buffer = buffer.Slice(message.End);
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

    HmonEvent? hmonEvent = command switch
    {
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

    if (hmonEvent != null)
    {
      var uid = GetUid(hmonEvent);
      if (uid != null && _pendingRequests.TryRemove(uid, out var tcs))
      {
        tcs.TrySetResult(hmonEvent);
      }
      else
      {
        await _eventWriter.WriteAsync(hmonEvent);
      }
    }
  }

  private string? GetUid(HmonEvent hmonEvent)
  {
    return hmonEvent switch
    {
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