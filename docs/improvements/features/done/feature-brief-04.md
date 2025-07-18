# Feature Brief #4: Add Configurable, Per-Command Timeouts to the Orchestrator

## Problem Statement

The public interaction methods on `HmonOrchestrator` (e.g., `GetFactsAsync`, `SubscribeAsync`) rely solely on a `CancellationToken` for timing out. If an interpreter becomes unresponsive in a way that doesn't close the TCP socket, a command sent without a CancellationToken (or with a long-lived one) can hang indefinitely, blocking the calling thread. The library should provide a built-in safety net against this scenario.

## Proposed Solution

We will introduce a default timeout at the orchestrator level and allow it to be overridden on a per-call basis.

1. **Configuration:** Add a default timeout property to `HmonOrchestratorOptions`.

    ```csharp
    // In HmonOrchestratorOptions.cs
    public record HmonOrchestratorOptions
    {
        // ... existing properties
        public TimeSpan DefaultCommandTimeout { get; init; } = TimeSpan.FromSeconds(30);
    }
    ```

2. **API Overloads:** Update the signatures of all asynchronous interaction methods to accept an optional `TimeSpan? timeout` parameter.

    ```csharp
    // Example in HmonOrchestrator.cs
    public Task<FactsResponse> GetFactsAsync(
        Guid sessionId,
        IEnumerable<FactType> facts,
        TimeSpan? timeout = null, // New parameter
        CancellationToken ct = default);
    ```

3. **Internal Implementation:** Internally, these methods will create a linked `CancellationTokenSource` that combines the user-provided `CancellationToken` with a new `CancellationTokenSource` set to the specified timeout (or the default).

    ```csharp
    // Example implementation detail
    var effectiveTimeout = timeout ?? _options.DefaultCommandTimeout;
    using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
    
    // Pass linkedCts.Token to SendCommandAsync
    var evt = await conn.SendCommandAsync<FactsReceivedEvent>("GetFacts", payload, linkedCts.Token);
    ```

    If the command times out, it will now throw an `OperationCanceledException` that can be caught and handled appropriately.

## API Changes

* **Added:** A new configuration property `HmonOrchestratorOptions.DefaultCommandTimeout`.
* **Added:** A new optional `TimeSpan? timeout` parameter to all relevant public methods: `GetFactsAsync`, `GetLastKnownStateAsync`, `PollFactsAsync`, `StopFactsPollingAsync`, `BumpFactsAsync`, `SubscribeAsync`, `ConnectRideAsync`, `DisconnectRideAsync`.

## Impact and Risks

* **Positive:** Greatly improves the resilience and predictability of the client library. It prevents application threads from hanging on unresponsive network endpoints and provides a clear, configurable timeout mechanism.
* **Negative:** None. This is a non-breaking, purely additive change to the API.

## Alternatives Considered

* **Relying solely on `CancellationToken`:** This was rejected because it places the full burden of timeout management on every single consumer, making the library harder to use safely. Providing a sensible default is a better practice.
