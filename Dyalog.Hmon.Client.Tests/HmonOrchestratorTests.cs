using Xunit;
using Dyalog.Hmon.Client.Lib;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

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
        await Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
        {
            await orchestrator.GetFactsAsync(System.Guid.NewGuid(), new[] { FactType.Host });
        });
    }

    [Fact]
    public async Task SubscribeAsync_ThrowsOnInvalidSession()
    {
        var orchestrator = new HmonOrchestrator();
        await Assert.ThrowsAsync<System.InvalidOperationException>(async () =>
        {
            await orchestrator.SubscribeAsync(System.Guid.NewGuid(), new[] { SubscriptionEvent.WorkspaceCompaction });
        });
    }
}
