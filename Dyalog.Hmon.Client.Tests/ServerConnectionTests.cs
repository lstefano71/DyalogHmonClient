using Xunit;
using Dyalog.Hmon.Client.Lib;
using System.Threading.Channels;

namespace Dyalog.Hmon.Client.Tests;

/// <summary>
/// Unit tests for ServerConnection core logic.
/// </summary>
public class ServerConnectionTests
{
    [Fact]
    public async Task CanConstructAndDisposeServerConnection()
    {
        var options = new HmonOrchestratorOptions();
        var writer = Channel.CreateUnbounded<HmonEvent>().Writer;
        var sessionId = System.Guid.NewGuid();
        var server = new ServerConnection("localhost", 12345, "test", options, writer, sessionId, null, null);
        await server.DisposeAsync();
    }
}
