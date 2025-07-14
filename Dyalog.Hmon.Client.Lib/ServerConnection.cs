using System.Net.Sockets;
using System.Threading.Channels;
using Serilog;
using System.Text;

namespace Dyalog.Hmon.Client.Lib;

/// <summary>
/// Manages a remote HMON server connection with retry and reconnection logic.
/// </summary>
internal class ServerConnection : IAsyncDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly string? _friendlyName;
    private readonly HmonOrchestratorOptions _options;
    private readonly ChannelWriter<HmonEvent> _eventWriter;
    private readonly Guid _sessionId;
    private readonly Func<ClientConnectedEventArgs, Task>? _onClientConnected;
    private readonly Func<ClientDisconnectedEventArgs, Task>? _onClientDisconnected;
    private readonly Action<HmonConnection>? _registerConnection;
    private readonly CancellationTokenSource _cts = new();
    private HmonConnection? _hmonConnection;

    /// <summary>
    /// Initializes a new ServerConnection and starts connection management.
    /// </summary>
    /// <param name="host">Remote host address.</param>
    /// <param name="port">Remote port.</param>
    /// <param name="friendlyName">Optional friendly name.</param>
    /// <param name="options">Orchestrator options.</param>
    /// <param name="eventWriter">Channel writer for events.</param>
    /// <param name="sessionId">Session identifier.</param>
    /// <param name="onClientConnected">Callback for client connected.</param>
    /// <param name="onClientDisconnected">Callback for client disconnected.</param>
    public ServerConnection(
        string host,
        int port,
        string? friendlyName,
        HmonOrchestratorOptions options,
        ChannelWriter<HmonEvent> eventWriter,
        Guid sessionId,
        Func<ClientConnectedEventArgs, Task>? onClientConnected,
        Func<ClientDisconnectedEventArgs, Task>? onClientDisconnected,
        Action<HmonConnection>? registerConnection = null)
    {
        _host = host;
        _port = port;
        _friendlyName = friendlyName;
        _options = options;
        _eventWriter = eventWriter;
        _sessionId = sessionId;
        _onClientConnected = onClientConnected;
        _onClientDisconnected = onClientDisconnected;
        _registerConnection = registerConnection;
        _ = ConnectWithRetriesAsync(_cts.Token);
    }

    private async Task ConnectWithRetriesAsync(CancellationToken ct)
    {
        var retryPolicy = _options.ConnectionRetryPolicy;
        var delay = retryPolicy.InitialDelay;
        var logger = Log.Logger.ForContext<ServerConnection>();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                logger.Debug("Attempting to connect to {Host}:{Port} (SessionId={SessionId})", _host, _port, _sessionId);
                var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(_host, _port, ct);

                logger.Information("Connection established to {Host}:{Port} (SessionId={SessionId})", _host, _port, _sessionId);

                // Perform handshake with the server before creating HmonConnection
                await PerformHandshakeAsync(tcpClient.GetStream(), ct);

                    // Only after handshake, consider connection established
                    _hmonConnection = new HmonConnection(tcpClient, _sessionId, _eventWriter, async () =>
                    {
                        logger.Debug("Connection closed for {Host}:{Port} (SessionId={SessionId}), attempting reconnect", _host, _port, _sessionId);
                        if (_onClientDisconnected != null)
                        {
                            await _onClientDisconnected.Invoke(new ClientDisconnectedEventArgs(_sessionId, _host, _port, _friendlyName, "Connection closed"));
                        }
                        await ConnectWithRetriesAsync(ct); // Reconnect
                    });
                
                    // Register the connection in the orchestrator
                    _registerConnection?.Invoke(_hmonConnection);

                if (_onClientConnected != null)
                {
                    await _onClientConnected.Invoke(new ClientConnectedEventArgs(_sessionId, _host, _port, _friendlyName));
                }

                // Wait for the HmonConnection to start processing before returning
                await Task.Delay(100, ct);

                return; // Connection successful, exit the retry loop.
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Connection attempt failed to {Host}:{Port} (SessionId={SessionId}), retrying in {Delay}ms", _host, _port, _sessionId, delay.TotalMilliseconds);
                if (_onClientDisconnected != null)
                {
                    await _onClientDisconnected.Invoke(new ClientDisconnectedEventArgs(_sessionId, _host, _port, _friendlyName, ex.Message));
                }

                await Task.Delay(delay, ct);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * retryPolicy.BackoffMultiplier);
                if (delay > retryPolicy.MaxDelay) delay = retryPolicy.MaxDelay;
            }
        }
    }

    /// <summary>
    /// Disposes the server connection and releases resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_hmonConnection != null)
        {
            await _hmonConnection.DisposeAsync();
        }
    }

    // Perform the HMON handshake protocol (2 receive/send pairs)
    private async Task PerformHandshakeAsync(NetworkStream stream, CancellationToken ct)
    {
        var logger = Log.Logger.ForContext<ServerConnection>();
        logger.Debug("Handshake: Sending SupportedProtocols=2");
        string supported = "SupportedProtocols=2";
        await SendHandshakeFrameAsync(stream, supported, ct);
        logger.Debug("Handshake: Waiting for SupportedProtocols=2 from server");
        await ReceiveHandshakeFrameAsync(stream, ct);
        logger.Debug("Handshake: Sending UsingProtocol=2");
        string usingProto = "UsingProtocol=2";
        await SendHandshakeFrameAsync(stream, usingProto, ct);
        logger.Debug("Handshake: Waiting for UsingProtocol=2 from server");
        await ReceiveHandshakeFrameAsync(stream, ct);
        logger.Debug("Handshake: Completed successfully");
    }

    private static async Task<string> ReceiveHandshakeFrameAsync(NetworkStream stream, CancellationToken ct)
    {
        var lengthBytes = new byte[4];
        await ReadExactAsync(stream, lengthBytes, ct);
        if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
        int totalLength = BitConverter.ToInt32(lengthBytes, 0);

        var magic = new byte[4];
        await ReadExactAsync(stream, magic, ct);
        if (!(magic[0] == 0x48 && magic[1] == 0x4D && magic[2] == 0x4F && magic[3] == 0x4E))
            throw new InvalidOperationException("Invalid handshake magic number");

        int payloadLength = totalLength - 8;
        var payloadBytes = new byte[payloadLength];
        await ReadExactAsync(stream, payloadBytes, ct);
        return Encoding.UTF8.GetString(payloadBytes);
    }

    private static async Task SendHandshakeFrameAsync(NetworkStream stream, string payload, CancellationToken ct)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var totalLength = 8 + payloadBytes.Length;
        var lengthBytes = BitConverter.GetBytes(totalLength);
        if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
        byte[] magic = [0x48, 0x4D, 0x4F, 0x4E];
        await stream.WriteAsync(lengthBytes, ct);
        await stream.WriteAsync(magic, ct);
        await stream.WriteAsync(payloadBytes, ct);
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct);
            if (read == 0) throw new InvalidOperationException("Unexpected end of stream during handshake");
            offset += read;
        }
    }
}
// End of ServerConnection class
