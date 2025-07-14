using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Minimal mock HMON server for integration testing.
/// Accepts a single connection and performs the DRP-T handshake.
/// </summary>
public class MockHmonServer : IDisposable
{
    private readonly TcpListener _listener;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly CancellationTokenSource _cts = new();

    public int Port { get; }

    public MockHmonServer(int port = 0)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
    }

    public async Task AcceptAndHandshakeAsync(CancellationToken ct = default)
    {
        Exception? lastException = null;
        var acceptStart = DateTime.UtcNow;
        var acceptTimeout = TimeSpan.FromSeconds(5);
        while (!ct.IsCancellationRequested)
        {
            if (DateTime.UtcNow - acceptStart > acceptTimeout)
                throw new TimeoutException("Timed out waiting for client connection in MockHmonServer.");
            Console.WriteLine("MockHmonServer: Waiting for client connection...");
            try
            {
                _client = await _listener.AcceptTcpClientAsync(ct);
                _stream = _client.GetStream();

                // Perform handshake: 4 messages, each framed with DRP-T and "HMON" magic
                for (int i = 0; i < 2; i++)
                {
                    await ReceiveHandshakeFrameAsync(_stream, i == 0 ? "SupportedProtocols=2" : "UsingProtocol=2", ct);
                    await SendHandshakeFrameAsync(_stream, i == 0 ? "SupportedProtocols=2" : "UsingProtocol=2", ct);
                }
                // Handshake succeeded, exit loop
                Console.WriteLine("MockHmonServer: Handshake succeeded, exiting loop.");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MockHmonServer: Exception in AcceptAndHandshakeAsync: {ex.Message}");
                lastException = ex;
                _stream?.Dispose();
                _client?.Dispose();
                _stream = null;
                _client = null;
                // Wait briefly before accepting another connection
                Console.WriteLine("MockHmonServer: Waiting 200ms before retrying...");
                await Task.Delay(200, ct);
            }
        }
        if (lastException != null)
            throw lastException;
    }

    /// <summary>
    /// Accepts a connection and simulates a handshake failure by sending an invalid handshake frame.
    /// </summary>
    public async Task AcceptAndFailHandshakeAsync(CancellationToken ct = default)
    {
        var acceptStart = DateTime.UtcNow;
        var acceptTimeout = TimeSpan.FromSeconds(5);
        while (!ct.IsCancellationRequested)
        {
            if (DateTime.UtcNow - acceptStart > acceptTimeout)
                throw new TimeoutException("Timed out waiting for client connection in MockHmonServer.");
            Console.WriteLine("MockHmonServer: Waiting for client connection (fail handshake)...");
            try
            {
                _client = await _listener.AcceptTcpClientAsync(ct);
                _stream = _client.GetStream();

                // Send an invalid handshake frame (wrong magic number)
                var payloadBytes = Encoding.UTF8.GetBytes("InvalidHandshake");
                var totalLength = 8 + payloadBytes.Length;
                var lengthBytes = BitConverter.GetBytes(totalLength);
                if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
                byte[] invalidMagic = [0x00, 0x00, 0x00, 0x00];
                await _stream.WriteAsync(lengthBytes, ct);
                await _stream.WriteAsync(invalidMagic, ct);
                await _stream.WriteAsync(payloadBytes, ct);

                // Close connection immediately
                _stream.Dispose();
                _client.Dispose();
                _stream = null;
                _client = null;
                Console.WriteLine("MockHmonServer: Sent invalid handshake and closed connection.");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MockHmonServer: Exception in AcceptAndFailHandshakeAsync: {ex.Message}");
                _stream?.Dispose();
                _client?.Dispose();
                _stream = null;
                _client = null;
                await Task.Delay(200, ct);
            }
        }
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

    private static async Task ReceiveHandshakeFrameAsync(NetworkStream stream, string expectedPayload, CancellationToken ct)
    {
        var lengthBytes = new byte[4];
        await ReadExactAsync(stream, lengthBytes, ct);
        if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
        int totalLength = BitConverter.ToInt32(lengthBytes, 0);
        
        var magic = new byte[4];
        await ReadExactAsync(stream, magic, ct);
        if (!(magic[0] == 0x48 && magic[1] == 0x4D && magic[2] == 0x4F && magic[3] == 0x4E))
            throw new InvalidOperationException("Invalid handshake magic number");
        
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
            if (read == 0) throw new Exception("Unexpected end of stream");
            offset += read;
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _stream?.Dispose();
        _client?.Dispose();
        _listener.Stop();
    }
}
