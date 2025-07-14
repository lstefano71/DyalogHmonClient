using Dyalog.Hmon.Client.Lib;

using Xunit;

namespace Dyalog.Hmon.Client.Tests;

/// <summary>
/// Integration tests for end-to-end HMON protocol flows.
/// </summary>
public class IntegrationTests
{
  [Fact]
  public async Task Handshake_Succeeds_WithMockServer()
  {
    using var mockServer = new MockHmonServer();
    var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
    var orchestrator = new HmonOrchestrator();

    // Start accepting in the background
    var acceptTask = mockServer.AcceptAndHandshakeAsync(cts.Token);

    // Connect orchestrator to mock server
    var sessionId = orchestrator.AddServer("127.0.0.1", mockServer.Port, "mock");

    // Wait for handshake to complete (should not throw)
    await acceptTask;

    // Cleanup
    await orchestrator.DisposeAsync();
  }

  [Fact]
  public async Task Handshake_Fails_WithInvalidHandshake()
  {
    using var mockServer = new MockHmonServer();
    var cts = new System.Threading.CancellationTokenSource(System.TimeSpan.FromSeconds(5));
    var orchestrator = new HmonOrchestrator();

    // Start failing handshake in the background
    var failTask = mockServer.AcceptAndFailHandshakeAsync(cts.Token);

    // Connect orchestrator to mock server
    var sessionId = orchestrator.AddServer("127.0.0.1", mockServer.Port, "mock");

    // Wait for the server to process the failed handshake
    await failTask;

    // The orchestrator should not expose the failed session
    await Assert.ThrowsAnyAsync<Exception>(async () => {
      await orchestrator.GetFactsAsync(sessionId, Array.Empty<Dyalog.Hmon.Client.Lib.FactType>(), default);
    });

    await orchestrator.DisposeAsync();
  }
}
