using System.Net.Sockets;
using System.Threading.Channels;

namespace Dyalog.Hmon.Client.Lib;

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
    private readonly CancellationTokenSource _cts = new();
    private HmonConnection? _hmonConnection;

    public ServerConnection(
        string host,
        int port,
        string? friendlyName,
        HmonOrchestratorOptions options,
        ChannelWriter<HmonEvent> eventWriter,
        Guid sessionId,
        Func<ClientConnectedEventArgs, Task>? onClientConnected,
        Func<ClientDisconnectedEventArgs, Task>? onClientDisconnected)
    {
        _host = host;
        _port = port;
        _friendlyName = friendlyName;
        _options = options;
        _eventWriter = eventWriter;
        _sessionId = sessionId;
        _onClientConnected = onClientConnected;
        _onClientDisconnected = onClientDisconnected;
        _ = ConnectWithRetriesAsync(_cts.Token);
    }

    private async Task ConnectWithRetriesAsync(CancellationToken ct)
    {
        var retryPolicy = _options.ConnectionRetryPolicy;
        var delay = retryPolicy.InitialDelay;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(_host, _port, ct);

                _hmonConnection = new HmonConnection(tcpClient, _sessionId, _eventWriter, async () =>
                {
                    if (_onClientDisconnected != null)
                    {
                        await _onClientDisconnected.Invoke(new ClientDisconnectedEventArgs(_sessionId, _host, _port, _friendlyName, "Connection closed"));
                    }
                    await ConnectWithRetriesAsync(ct); // Reconnect
                });

                if (_onClientConnected != null)
                {
                    await _onClientConnected.Invoke(new ClientConnectedEventArgs(_sessionId, _host, _port, _friendlyName));
                }

                return; // Connection successful, exit the retry loop.
            }
            catch (Exception ex)
            {
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

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_hmonConnection != null)
        {
            await _hmonConnection.DisposeAsync();
        }
    }
}
