using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Serilog;

namespace Dyalog.Hmon.Client.Lib;

/// <summary>
/// Represents a single HMON protocol connection, handling framing, handshake, and event dispatch.
/// </summary>
internal class HmonConnection : IAsyncDisposable
{
    private readonly TcpClient _tcpClient;

    // --- Handshake implementation ---

    internal async Task PerformHandshakeAsync(CancellationToken ct)
    {
        var stream = _tcpClient.GetStream();
        // Handshake sequence: SupportedProtocols=2 (send/recv), UsingProtocol=2 (send/recv)
        await SendHandshakeFrameAsync(stream, "SupportedProtocols=2", ct);
        await ReceiveHandshakeFrameAsync(stream, "SupportedProtocols=2", ct);
        await SendHandshakeFrameAsync(stream, "UsingProtocol=2", ct);
        await ReceiveHandshakeFrameAsync(stream, "UsingProtocol=2", ct);
    }

    private static async Task SendHandshakeFrameAsync(NetworkStream stream, string payload, CancellationToken ct)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var totalLength = 8 + payloadBytes.Length;
        var lengthBytes = BitConverter.GetBytes(totalLength);
        if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);

        // Magic number for HMON: 0x48 0x4D 0x4F 0x4E ("HMON")
        byte[] magic = { 0x48, 0x4D, 0x4F, 0x4E };

        await stream.WriteAsync(lengthBytes, ct);
        await stream.WriteAsync(magic, ct);
        await stream.WriteAsync(payloadBytes, ct);
    }

    private static async Task ReceiveHandshakeFrameAsync(NetworkStream stream, string expectedPayload, CancellationToken ct)
    {
        // Read total length (4 bytes)
        var lengthBytes = new byte[4];
        await ReadExactAsync(stream, lengthBytes, ct);
        if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
        int totalLength = BitConverter.ToInt32(lengthBytes, 0);

        // Read magic (4 bytes)
        var magic = new byte[4];
        await ReadExactAsync(stream, magic, ct);
        if (!(magic[0] == 0x48 && magic[1] == 0x4D && magic[2] == 0x4F && magic[3] == 0x4E))
            throw new InvalidOperationException("Invalid handshake magic number");

        // Read payload
        int payloadLen = totalLength - 8;
        var payloadBytes = new byte[payloadLen];
        await ReadExactAsync(stream, payloadBytes, ct);
        var payload = Encoding.UTF8.GetString(payloadBytes);
        if (payload != expectedPayload)
            throw new InvalidOperationException($"Handshake payload mismatch. Expected '{expectedPayload}', got '{payload}'");
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct);
            if (read == 0) throw new IOException("Unexpected end of stream during handshake");
            offset += read;
        }
    }
    private readonly Guid _sessionId;
    private readonly ChannelWriter<HmonEvent> _eventWriter;
    private readonly Func<Task>? _onDisconnect;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<HmonEvent>> _pendingRequests = new();

    /// <summary>
    /// Initializes a new HmonConnection for the given TCP client and session.
    /// </summary>
    /// <param name="tcpClient">Underlying TCP client.</param>
    /// <param name="sessionId">Session identifier.</param>
    /// <param name="eventWriter">Channel writer for events.</param>
    /// <param name="onDisconnect">Optional disconnect callback.</param>
    public HmonConnection(TcpClient tcpClient, Guid sessionId, ChannelWriter<HmonEvent> eventWriter, Func<Task>? onDisconnect = null)
    {
        _tcpClient = tcpClient;
        _sessionId = sessionId;
        _eventWriter = eventWriter;
        _onDisconnect = onDisconnect;
        _ = StartProcessingAsync(_cts.Token);
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
        var uid = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<HmonEvent>();
        _pendingRequests.TryAdd(uid, tcs);

        var stream = _tcpClient.GetStream();
        var json = JsonSerializer.Serialize(new object[] { command, payload });
        var bytes = Encoding.UTF8.GetBytes(json);
        var length = BitConverter.GetBytes(bytes.Length);
        if (BitConverter.IsLittleEndian) Array.Reverse(length);

        await stream.WriteAsync(length, ct);
        await stream.WriteAsync(bytes, ct);

        using (ct.Register(() => tcs.TrySetCanceled()))
        {
            var result = await tcs.Task;
            return (T)result;
        }
    }

    private async Task StartProcessingAsync(CancellationToken ct)
    {
        try
        {
            var pipe = new Pipe();
            Task writing = FillPipeAsync(_tcpClient.GetStream(), pipe.Writer, ct);
            Task reading = ReadPipeAsync(pipe.Reader, ct);
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
        while (!ct.IsCancellationRequested)
        {
            ReadResult result = await reader.ReadAsync(ct);
            ReadOnlySequence<byte> buffer = result.Buffer;

            while (TryReadMessage(ref buffer, out var message))
            {
                await ParseAndDispatchMessageAsync(message);
            }

            reader.AdvanceTo(buffer.Start, buffer.End);
            if (result.IsCompleted) break;
        }
        await reader.CompleteAsync();
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
