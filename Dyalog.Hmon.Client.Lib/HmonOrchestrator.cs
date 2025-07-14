using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace Dyalog.Hmon.Client.Lib;

public class HmonOrchestrator : IAsyncDisposable
{
    private readonly HmonOrchestratorOptions _options;
    private readonly Channel<HmonEvent> _eventChannel = Channel.CreateUnbounded<HmonEvent>();
    private readonly ConcurrentDictionary<Guid, HmonConnection> _connections = new();
    private readonly ConcurrentDictionary<Guid, ServerConnection> _servers = new();

    public HmonOrchestrator(HmonOrchestratorOptions? options = null)
    {
        _options = options ?? new HmonOrchestratorOptions();
    }

    public IAsyncEnumerable<HmonEvent> Events => _eventChannel.Reader.ReadAllAsync();

    public event Func<ClientConnectedEventArgs, Task>? ClientConnected;
    public event Func<ClientDisconnectedEventArgs, Task>? ClientDisconnected;

    public async Task StartListenerAsync(string host, int port, CancellationToken ct = default)
    {
        var listener = new TcpListener(IPAddress.Parse(host), port);
        listener.Start();
        ct.Register(() => listener.Stop());

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var tcpClient = await listener.AcceptTcpClientAsync(ct);
                var sessionId = Guid.NewGuid();
                var connection = new HmonConnection(tcpClient, sessionId, _eventChannel.Writer, async () =>
                {
                    _connections.TryRemove(sessionId, out _);
                    if (ClientDisconnected != null)
                    {
                        var remoteEndPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint!;
                        await ClientDisconnected.Invoke(new ClientDisconnectedEventArgs(sessionId, remoteEndPoint.Address.ToString(), remoteEndPoint.Port, null, "Remote client disconnected"));
                    }
                });
                _connections.TryAdd(sessionId, connection);

                // Perform handshake before invoking ClientConnected
                try
                {
                    await connection.PerformHandshakeAsync(ct);
                }
                catch (Exception ex)
                {
                    // Handshake failed: clean up and do not fire ClientConnected
                    _connections.TryRemove(sessionId, out _);
                    await connection.DisposeAsync();
                    continue;
                }

                if (ClientConnected != null)
                {
                    var remoteEndPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint!;
                    await ClientConnected.Invoke(new ClientConnectedEventArgs(sessionId, remoteEndPoint.Address.ToString(), remoteEndPoint.Port, null));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the cancellation token is triggered.
        }
        finally
        {
            listener.Stop();
        }
    }

    public Guid AddServer(string host, int port, string? friendlyName = null)
    {
        var sessionId = Guid.NewGuid();
        var server = new ServerConnection(host, port, friendlyName, _options, _eventChannel.Writer, sessionId,
            (args) => ClientConnected?.Invoke(args),
            (args) => ClientDisconnected?.Invoke(args));
        _servers.TryAdd(sessionId, server);
        return sessionId;
    }

    public async Task RemoveServerAsync(Guid sessionId)
    {
        if (_servers.TryRemove(sessionId, out var server))
        {
            await server.DisposeAsync();
        }
    }

    public async Task<FactsResponse> GetFactsAsync(Guid sessionId, IEnumerable<FactType> facts, CancellationToken ct = default)
    {
        if (!_connections.TryGetValue(sessionId, out var conn))
            throw new InvalidOperationException("Session not found");
        var payload = new
        {
            Facts = facts.Select(f => (int)f).ToArray(),
            UID = Guid.NewGuid().ToString()
        };
        var evt = await conn.SendCommandAsync<FactsReceivedEvent>("GetFacts", payload, ct);
        return evt.Facts;
    }

    public async Task<LastKnownStateResponse> GetLastKnownStateAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (!_connections.TryGetValue(sessionId, out var conn))
            throw new InvalidOperationException("Session not found");
        var payload = new { UID = Guid.NewGuid().ToString() };
        var evt = await conn.SendCommandAsync<LastKnownStateReceivedEvent>("GetLastKnownState", payload, ct);
        return evt.State;
    }

    public async Task PollFactsAsync(Guid sessionId, IEnumerable<FactType> facts, TimeSpan interval, CancellationToken ct = default)
    {
        if (!_connections.TryGetValue(sessionId, out var conn))
            throw new InvalidOperationException("Session not found");
        var payload = new
        {
            Facts = facts.Select(f => (int)f).ToArray(),
            Interval = (int)interval.TotalMilliseconds,
            UID = Guid.NewGuid().ToString()
        };
        await conn.SendCommandAsync<FactsReceivedEvent>("PollFacts", payload, ct);
    }

    public async Task StopFactsPollingAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (!_connections.TryGetValue(sessionId, out var conn))
            throw new InvalidOperationException("Session not found");
        var payload = new { };
        await conn.SendCommandAsync<FactsReceivedEvent>("StopFacts", payload, ct);
    }

    public async Task BumpFactsAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (!_connections.TryGetValue(sessionId, out var conn))
            throw new InvalidOperationException("Session not found");
        var payload = new { };
        await conn.SendCommandAsync<FactsReceivedEvent>("BumpFacts", payload, ct);
    }

    public async Task SubscribeAsync(Guid sessionId, IEnumerable<SubscriptionEvent> events, CancellationToken ct = default)
    {
        if (!_connections.TryGetValue(sessionId, out var conn))
            throw new InvalidOperationException("Session not found");
        var payload = new
        {
            Events = events.Select(e => (int)e).ToArray(),
            UID = Guid.NewGuid().ToString()
        };
        await conn.SendCommandAsync<SubscribedResponseReceivedEvent>("Subscribe", payload, ct);
    }

    public async Task<RideConnectionResponse> ConnectRideAsync(Guid sessionId, string address, int port, CancellationToken ct = default)
    {
        if (!_connections.TryGetValue(sessionId, out var conn))
            throw new InvalidOperationException("Session not found");
        var payload = new
        {
            Address = address,
            Port = port,
            UID = Guid.NewGuid().ToString()
        };
        var evt = await conn.SendCommandAsync<RideConnectionReceivedEvent>("ConnectRide", payload, ct);
        return evt.Response;
    }

    public async Task<RideConnectionResponse> DisconnectRideAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (!_connections.TryGetValue(sessionId, out var conn))
            throw new InvalidOperationException("Session not found");
        var payload = new { };
        var evt = await conn.SendCommandAsync<RideConnectionReceivedEvent>("ConnectRide", payload, ct);
        return evt.Response;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var server in _servers.Values)
        {
            await server.DisposeAsync();
        }
        _servers.Clear();

        foreach (var connection in _connections.Values)
        {
            await connection.DisposeAsync();
        }
        _connections.Clear();

        _eventChannel.Writer.Complete();
        await _eventChannel.Reader.Completion;
    }
}
