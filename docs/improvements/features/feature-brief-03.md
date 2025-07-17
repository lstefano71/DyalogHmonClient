# Feature Brief #3: Implement a Fact Caching Policy with TTL

## Problem Statement

The `HmonOrchestrator._factCache` provides a valuable performance enhancement by caching the latest facts from each session. However, it lacks an invalidation or expiration mechanism. If polling stops for a given session (either intentionally or due to a prolonged disconnection), the cache will hold onto its last known values indefinitely. The public `GetFact<T>()` method will then continue to serve this stale data to consumers, which can lead to incorrect monitoring dashboards and faulty application logic.

## Proposed Solution

We will implement a simple Time-To-Live (TTL) check on the cached facts to ensure consumers do not receive outdated information.

1. **Configuration:** Add a new property to the `HmonOrchestratorOptions` record to make the TTL configurable.

    ```csharp
    // In HmonOrchestratorOptions.cs
    public record HmonOrchestratorOptions
    {
        // ... existing properties
        public TimeSpan FactCacheTTL { get; init; } = TimeSpan.FromMinutes(5);
    }
    ```

2. **Read-Time Invalidation:** Modify the `GetFact` and `GetFactWithTimestamp` methods in `HmonOrchestrator.cs`. Before returning a cached fact, these methods will perform a check against the `LastUpdated` timestamp stored in the `FactCacheEntry`.

    ```csharp
    // In HmonOrchestrator.cs
    public T? GetFact<T>(Guid sessionId) where T : Fact
    {
        if (_factCache.TryGetValue((sessionId, typeof(T)), out var entry))
        {
            if (DateTimeOffset.UtcNow - entry.LastUpdated > _options.FactCacheTTL)
            {
                // Fact is stale, do not return it.
                // Optionally, we could also remove it from the cache here.
                _factCache.TryRemove((sessionId, typeof(T)), out _);
                return null;
            }
            return entry.Fact as T;
        }
        return null;
    }
    ```

    This approach is efficient as it avoids a background timer, performing the check only when data is requested. It also self-heals by removing the expired entry.

## API Changes

* **Added:** A new configuration property `HmonOrchestratorOptions.FactCacheTTL`.
* **Modified (Behavioral):** The `GetFact<T>()` and `GetFactWithTimestamp()` methods will now return `null` if the cached fact is older than the configured TTL. This is a subtle but important behavioral change that enhances correctness.

## Impact and Risks

* **Positive:** Prevents consumers from acting on stale data, significantly improving the correctness and reliability of any application built on the library.
* **Negative:** A minor behavioral breaking change. Consumers who assumed `GetFact<T>()` would always return a value after the first poll will now need to handle `null` responses for expired data. This is a positive change for correctness and should be highlighted in release notes.

## Alternatives Considered

* **Background Scavenger Task:** A `Timer` could periodically scan the `_factCache` and remove expired items. This was rejected as being more complex and resource-intensive than the simple, on-demand check-on-read approach, which achieves the same goal with lower overhead.
