using Dyalog.Hmon.Client.Lib;
using Dyalog.Hmon.Client.Lib.Exceptions;

using Xunit;

namespace Dyalog.Hmon.Client.Tests;

/// <summary>
/// Unit tests for HmonOrchestrator core logic.
/// </summary>
public class HmonOrchestratorTests
{
  [Fact]
  public void CanConstructOrchestrator()
  {
    var orchestrator = new HmonOrchestrator();
    Assert.NotNull(orchestrator);
  }

  [Fact]
  public void EventsProperty_IsAsyncEnumerable()
  {
    var orchestrator = new HmonOrchestrator();
    Assert.IsAssignableFrom<IAsyncEnumerable<HmonEvent>>(orchestrator.Events);
  }

  [Fact]
  public async Task DisposeAsync_CompletesWithoutError()
  {
    var orchestrator = new HmonOrchestrator();
    await orchestrator.DisposeAsync();
  }

  [Fact]
  public async Task RemoveServerAsync_DoesNotThrow_WhenSessionNotFound()
  {
    var orchestrator = new HmonOrchestrator();
    var randomSession = System.Guid.NewGuid();
    await orchestrator.RemoveServerAsync(randomSession);
  }

  [Fact]
  public async Task GetFactsAsync_ThrowsOnInvalidSession()
  {
    var orchestrator = new HmonOrchestrator();
    await Assert.ThrowsAsync<SessionNotFoundException>(async () => {
      await orchestrator.GetFactsAsync(System.Guid.NewGuid(), [FactType.Host]);
    });
  }

  [Fact]
  public async Task SubscribeAsync_ThrowsOnInvalidSession()
  {
    var orchestrator = new HmonOrchestrator();
    await Assert.ThrowsAsync<SessionNotFoundException>(async () => {
      await orchestrator.SubscribeAsync(System.Guid.NewGuid(), [SubscriptionEvent.WorkspaceCompaction]);
    });
  }
}
