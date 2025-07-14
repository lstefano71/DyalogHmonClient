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
    using var tcpClient = new TcpClient();
    var writer = Channel.CreateUnbounded<HmonEvent>().Writer;
    var sessionId = System.Guid.NewGuid();
    var conn = new HmonConnection(tcpClient, sessionId, writer, null);
    await conn.DisposeAsync();
  }

  [Fact]
  public async Task SendCommandAsync_ThrowsOnClosedTcpClient()
  {
    using var tcpClient = new TcpClient();
    tcpClient.Close();
    var writer = Channel.CreateUnbounded<HmonEvent>().Writer;
    var sessionId = System.Guid.NewGuid();
    var conn = new HmonConnection(tcpClient, sessionId, writer, null);

    await Assert.ThrowsAsync<System.ObjectDisposedException>(async () => {
      await conn.SendCommandAsync<HmonEvent>("Test", new { }, default);
    });
  }
}
