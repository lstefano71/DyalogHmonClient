using Serilog;
using Polly;
using Polly.Retry;
using System.Net.Sockets;
using System.IO;
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
    _connectionTask = ConnectWithRetriesAsync(_cts.Token); // Track the task
  }

  private async Task ConnectWithRetriesAsync(CancellationToken ct)
  {
    var retryPolicy = _options.ConnectionRetryPolicy;
    var logger = Log.Logger.ForContext<ServerConnection>();

    // Polly retry policy with exponential backoff and jitter
    AsyncRetryPolicy retryPolicyWithJitter = Policy
      .Handle<SocketException>()
      .Or<IOException>()
      .Or<OperationCanceledException>(ex => !ct.IsCancellationRequested)
      .WaitAndRetryAsync(
        retryCount: int.MaxValue,
        sleepDurationProvider: attempt =>
        {
          // Exponential backoff with jitter
          var baseDelay = retryPolicy.InitialDelay.TotalMilliseconds * Math.Pow(retryPolicy.BackoffMultiplier, attempt - 1);
          var cappedDelay = Math.Min(baseDelay, retryPolicy.MaxDelay.TotalMilliseconds);
          var jitter = Random.Shared.NextDouble() * cappedDelay * 0.2; // up to 20% jitter
          return TimeSpan.FromMilliseconds(cappedDelay + jitter);
        },
        onRetry: (exception, timespan, attempt, context) =>
        {
          logger.Error(exception, "Connection attempt {Attempt} failed to {Host}:{Port} (SessionId={SessionId}), retrying in {Delay}ms",
            attempt, _host, _port, _sessionId, timespan.TotalMilliseconds);
        }
      );

    await retryPolicyWithJitter.ExecuteAsync(async () =>
    {
      ct.ThrowIfCancellationRequested();
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
      await _hmonConnection.InitializeAsync(_host, _port, _friendlyName, ct);

      // If we reach here, connection is successful; break retry loop by returning
    });
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
// End of ServerConnection class
