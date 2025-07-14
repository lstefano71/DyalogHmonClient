using Serilog;

using System.Net.Sockets;
using System.Threading.Channels;

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

    while (!ct.IsCancellationRequested) {
      try {
        logger.Debug("Attempting to connect to {Host}:{Port} (SessionId={SessionId})", _host, _port, _sessionId);
        var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(_host, _port, ct);

        logger.Information("Connection established to {Host}:{Port} (SessionId={SessionId})", _host, _port, _sessionId);

        // Only after handshake, consider connection established
        _hmonConnection = new HmonConnection(
            tcpClient,
            _sessionId,
            _eventWriter,
            async () => {
              logger.Debug("Connection closed for {Host}:{Port} (SessionId={SessionId}), attempting reconnect", _host, _port, _sessionId);
              if (_onClientDisconnected != null) {
                await _onClientDisconnected.Invoke(new ClientDisconnectedEventArgs(_sessionId, _host, _port, _friendlyName, "Connection closed"));
              }
              await ConnectWithRetriesAsync(ct); // Reconnect
            },
            _onClientConnected // Pass the ClientConnected event handler
        );

        // Register the connection in the orchestrator before firing ClientConnected
        _registerConnection?.Invoke(_hmonConnection);

        // Initialize the HmonConnection, which will also fire ClientConnected
        await _hmonConnection.InitializeAsync(ct, _host, _port, _friendlyName);

        return; // Connection successful, exit the retry loop.
      } catch (Exception ex) {
        logger.Error(ex, "Connection attempt failed to {Host}:{Port} (SessionId={SessionId}), retrying in {Delay}ms", _host, _port, _sessionId, delay.TotalMilliseconds);
        if (_onClientDisconnected != null) {
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
    if (_hmonConnection != null) {
      await _hmonConnection.DisposeAsync();
    }
  }

}
// End of ServerConnection class
