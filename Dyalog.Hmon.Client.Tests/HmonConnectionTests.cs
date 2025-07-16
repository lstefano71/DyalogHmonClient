using Dyalog.Hmon.Client.Lib;

using System.Net.Sockets;
using System.Threading.Channels;

using Xunit;

namespace Dyalog.Hmon.Client.Tests;

/// <summary>
/// Unit tests for HmonConnection core logic.
/// </summary>
public class HmonConnectionTests
{
  [Fact]
  public async Task CanConstructAndDisposeHmonConnection()
  {
    var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
    listener.Start();
    var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    var tcpClient = new TcpClient();
    await tcpClient.ConnectAsync("127.0.0.1", port);
    var _ = await listener.AcceptTcpClientAsync();
    var writer = Channel.CreateUnbounded<HmonEvent>().Writer;
    var sessionId = System.Guid.NewGuid();
    var conn = new HmonConnection(tcpClient, sessionId, writer, null);
    await conn.DisposeAsync();
    tcpClient.Dispose();
    listener.Stop();
  }

}
