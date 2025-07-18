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
  private readonly Action<HmonConnection>? _registerConnection;
  private readonly CancellationTokenSource _cts = new();
  private HmonConnection? _hmonConnection;
  private readonly Task? _connectionTask; // Track the background connection task
  /// <summary>
  /// Initializes a new ServerConnection and starts connection management.
  /// </summary>
  /// <param name="host">Remote host address.</param>
  /// <param name="port">Remote port.</param>
  /// <param name="friendlyName">Optional friendly name.</param>
  /// <param name="options">Orchestrator options.</param>
  /// <param name="eventWriter">Channel writer for events.</param>
  /// <param name="sessionId">Session identifier.</param>
  /// <param name="registerConnection">Callback to register the connection in the orchestrator.</param>
  public ServerConnection(
      string host,
      int port,
      string? friendlyName,
      HmonOrchestratorOptions options,
      ChannelWriter<HmonEvent> eventWriter,
      Guid sessionId,
      Action<HmonConnection>? registerConnection = null)
  {
    _host = host;
    _port = port;
    _friendlyName = friendlyName;
    _options = options;
    _eventWriter = eventWriter;
    _sessionId = sessionId;
    _registerConnection = registerConnection;
    _connectionTask = ConnectWithRetriesAsync(_cts.Token); // Track the task
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

        _hmonConnection = new HmonConnection(
            tcpClient,
            _sessionId,
            _eventWriter,
            async (reason) => {
              logger.Debug("Connection closed for {Host}:{Port} (SessionId={SessionId}), attempting reconnect", _host, _port, _sessionId);
              await _eventWriter.WriteAsync(new SessionDisconnectedEvent(_sessionId, _host, _port, _friendlyName, reason));
              // The ConnectWithRetriesAsync will be re-invoked by the loop's continuation
            }
        );
        _registerConnection?.Invoke(_hmonConnection);
        await _hmonConnection.InitializeAsync(_host, _port, _friendlyName, ct);

        // Wait here until the connection is closed. The onDisconnect callback will trigger the reconnect.
        await _hmonConnection.Completion;

      } catch (Exception ex) {
        logger.Error(ex, "Connection attempt failed to {Host}:{Port} (SessionId={SessionId}), retrying in {Delay}ms", _host, _port, _sessionId, delay.TotalMilliseconds);
        await _eventWriter.WriteAsync(new SessionDisconnectedEvent(_sessionId, _host, _port, _friendlyName, ex.Message), ct);

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
    if (_connectionTask != null) {
      try {
        await _connectionTask;
      } catch (OperationCanceledException) {
        // Expected on shutdown
      }
    }
  }
}
