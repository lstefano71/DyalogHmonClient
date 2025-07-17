using Serilog;
using Polly;
using Polly.Retry;
using System.Net.Sockets;
using System.IO;
using System.Threading.Channels;
using Dyalog.Hmon.Client.Lib.Exceptions;

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
  private readonly Action<HmonConnection> _registerConnection;
  private readonly CancellationTokenSource _cts = new();
  private HmonConnection? _hmonConnection;
  private readonly Task _connectionTask;
  public ServerConnection(
      string host,
      int port,
      string? friendlyName,
      HmonOrchestratorOptions options,
      ChannelWriter<HmonEvent> eventWriter,
      Guid sessionId,
      Action<HmonConnection> registerConnection)
  {
    _host = host;
    _port = port;
    _friendlyName = friendlyName;
    _options = options;
    _eventWriter = eventWriter;
    _sessionId = sessionId;
    _registerConnection = registerConnection;
    _connectionTask = ConnectWithRetriesAsync(_cts.Token);
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

    try
    {
      await retryPolicyWithJitter.ExecuteAsync(async () =>
      {
        ct.ThrowIfCancellationRequested();
        logger.Debug("Attempting to connect to {Host}:{Port} (SessionId={SessionId})", _host, _port, _sessionId);
        var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(_host, _port, ct);

        logger.Information("Connection established to {Host}:{Port} (SessionId={SessionId})", _host, _port, _sessionId);

        _hmonConnection = new HmonConnection(
            tcpClient,
            _sessionId,
            _eventWriter,
            async (reason) =>
            {
              logger.Debug("Connection closed for {Host}:{Port} (SessionId={SessionId}), reason: {Reason}", _host, _port, _sessionId, reason);
              await _eventWriter.WriteAsync(new SessionDisconnectedEvent(_sessionId, _host, _port, _friendlyName, reason));
            }
        );
        _registerConnection(_hmonConnection);
        await _hmonConnection.InitializeAsync(_host, _port, _friendlyName, ct);

        // Wait here until the connection terminates.
        await _hmonConnection.Completion;
      }
      catch (Exception ex) when (ex is not OperationCanceledException)
      {
        logger.Error(ex, "Connection attempt failed to {Host}:{Port} (SessionId={SessionId}), retrying in {Delay}ms", _host, _port, _sessionId, delay.TotalMilliseconds);
        await _eventWriter.WriteAsync(new SessionDisconnectedEvent(_sessionId, _host, _port, _friendlyName, ex.Message));
      }

      // If we are here, the connection has either failed or has been closed.
      // Wait for the delay before the next iteration of the loop tries to reconnect.
      if (!ct.IsCancellationRequested)
      {
        try
        {
          await Task.Delay(delay, ct);
          delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * retryPolicy.BackoffMultiplier);
          if (delay > retryPolicy.MaxDelay) delay = retryPolicy.MaxDelay;
        }
        catch (OperationCanceledException) { /* Loop will terminate */ }
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
    try { await _connectionTask; }
    catch (OperationCanceledException) { /* Expected */ }
  }
}
