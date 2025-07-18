### **TODO List: Project Enhancements**

This list is prioritized to tackle the most critical correctness issues first, followed by improvements that offer the most significant gains in robustness and maintainability.

**Phase 1: Critical Bug Fixes (Adapter Correctness)**

- [ ] **Issue #1:** Fix incorrect metric types in the OTEL adapter (Gauges to Counters).
- [ ] **Issue #2:** Fix memory leak and stale data by removing disconnected sessions from the metrics dictionary.
- [ ] **Issue #3:** Fix incomplete log enrichment for signal notifications.
- [ ] **Issue #4:** Implement configuration for polling and subscription settings, removing hardcoded values.

**Phase 2: Robustness and API Improvements (Library & Adapter)**

- [ ] **Feature Brief #1:** Introduce the Polly library for resilient connection retries.
- [ ] **Feature Brief #2:** Implement and use typed, specific exceptions for error handling.
- [ ] **Feature Brief #3:** Implement a fact caching policy with TTL to prevent stale data.

**Phase 3: Long-Term Architectural Refactoring (Library)**

- [ ] **ADR #1:** Refactor the `HmonOrchestrator` to use a single, unified event stream, deprecating the dual-event model. *(Note: This is a breaking change and should be scheduled for a major version release, e.g., v2.0).*

**Phase 4: Code Quality and Developer Experience**

- [ ] **Feature Brief #4:** Add configurable, per-command timeouts to the orchestrator.
- [ ] **Feature Brief #5:** Refactor OTEL logging to use Serilog enrichers instead of custom extension methods.

---

### **Artifacts**

Here are the detailed documents for each item.

### **GitHub Issues**

---

#### **Issue #1: Bug: OTEL Adapter uses `ObservableGauge` for cumulative metrics**

**Labels:** `bug`, `high-priority`, `otel-adapter`

**Problem:**
The `Dyalog.Hmon.OtelAdapter.AdapterService` currently reports all metrics, including cumulative ones like `WorkspaceFact.Compactions` and `AccountInformationFact.ComputeTime`, using `ObservableGauge`. A gauge represents a single, point-in-time value. This is incorrect for values that only ever increase.

**Impact:**
Monitoring platforms (Prometheus, Grafana, etc.) will misinterpret these metrics. They cannot calculate rates or display them as ever-increasing counters, leading to fundamentally incorrect observability data.

**Proposed Solution:**

1. Identify all metrics in the PRD (`docs/hmon-to-otel-adapter-PRD.md`) that are specified as `Counter`.
2. In `AdapterService.cs`, change the instrument creation for these metrics from `_meter.CreateObservableGauge(...)` to `_meter.CreateObservableCounter(...)`.
3. Adjust the measurement logic accordingly. An `ObservableCounter` reports the total accumulated value.

**Acceptance Criteria:**
- Metrics like `dyalog.workspace.compactions` and `dyalog.cpu.time` must be exported as OTel `Sum` (Counter) types.
- Verification can be done by inspecting the output of the OTel `debug` exporter or by querying the metric in a tool like Prometheus.

---

#### **Issue #2: Bug: Memory leak and stale data from disconnected OTEL adapter sessions**

**Labels:** `bug`, `high-priority`, `memory-leak`, `otel-adapter`

**Problem:**
The `AdapterService._sessionMetrics` dictionary, which stores metric values for each session, never removes entries. When a Dyalog interpreter disconnects, its `SessionId` and last known metric values remain in the dictionary indefinitely.

**Impact:**

1. **Memory Leak:** The service's memory usage will grow continuously in any environment where interpreter sessions are not permanent.
2. **Stale Data:** The adapter will continue to export the last-known metric values for disconnected sessions, polluting dashboards with incorrect, stale data.

**Proposed Solution:**

1. In `AdapterService.cs`, ensure the `ClientDisconnected` event handler is correctly wired up.
2. Within the event handler lambda, call `_sessionMetrics.TryRemove(args.SessionId.ToString(), out _)` to delete the entry for the disconnected session.

**Acceptance Criteria:**
- When a client disconnects, its corresponding entry must be removed from the `_sessionMetrics` dictionary.
- After a client disconnects, its metrics should no longer appear in the OTel export stream.

---

#### **Issue #3: Bug: Incomplete log enrichment for `UntrappedSignal` and `TrappedSignal` events**

**Labels:** `bug`, `logging`, `otel-adapter`

**Problem:**
The PRD for the OTEL adapter specifies that `UntrappedSignal` and `TrappedSignal` log records must be enriched with detailed thread, stack, and DMX information. This requires fetching the full `ThreadsFact` for the relevant thread *after* receiving the notification. The current implementation in `HandleNotificationReceivedEventAsync` fails to do this, logging only the minimal data available in the notification packet itself.

**Impact:**
Logs for critical errors are missing essential diagnostic information, significantly reducing their value for debugging.

**Proposed Solution:**

1. Modify `HandleNotificationReceivedEventAsync` in `AdapterService.cs`.
2. Inside the `case` for `"UntrappedSignal"` and `"TrappedSignal"`, after receiving the `notificationEvent`, use the `_orchestrator` to make a follow-up call: `await _orchestrator.GetFactsAsync(notificationEvent.SessionId, new[] { FactType.Threads }, stoppingToken)`.
3. Find the specific `ThreadInfo` object from the response that matches `notificationEvent.Notification.Tid`.
4. Add the full `Stack`, `DMX`, and `Exception` details from that `ThreadInfo` object to the log attributes before writing the log message.

**Acceptance Criteria:**
- A log generated for an `UntrappedSignal` must contain attributes for the full stack trace and DMX info.
- The implementation must handle cases where the follow-up `GetFactsAsync` call might fail.

---

#### **Issue #4: Bug: Polling and subscription settings are hardcoded in the adapter**

**Labels:** `bug`, `config`, `otel-adapter`

**Problem:**
The `AdapterService` hardcodes the list of facts to poll and events to subscribe to when a new client connects. This ignores the `monitoring.pollIntervalSeconds` and `monitoring.subscribedEvents` sections defined in the PRD for the `config.json` file.

**Impact:**
The adapter is not flexible. Users cannot tailor the data collection to their needs (e.g., reducing polling frequency or subscribing only to critical events) without modifying the source code.

**Proposed Solution:**

1. In `AdapterService.ExecuteAsync`, read the polling interval and subscribed event names from the `_adapterConfig` object.
2. Convert the string names from the configuration into their corresponding `FactType` and `SubscriptionEvent` enum values. Provide a safe default if the configuration is missing.
3. Use these configurable lists when calling `_orchestrator.PollFactsAsync(...)` and `_orchestrator.SubscribeAsync(...)`.

**Acceptance Criteria:**
- The adapter must respect the `pollIntervalSeconds` and `subscribedEvents` arrays from `config.json`.
- If these settings are omitted from the config, the adapter should apply a sensible default (e.g., poll key facts every 15s, subscribe to all signals).

---

### **Feature Briefs**

---

#### **Feature Brief #1: Introduce Polly for Resilient Connection Retries**

**Problem Statement:**
The current connection retry logic in `Dyalog.Hmon.Client.Lib/ServerConnection.cs` is implemented with a manual `Task.Delay` and a backoff multiplier. This is a common pattern that is simple but lacks robustness. It does not include jitter, which can lead to synchronized retries in a thundering herd problem if multiple clients disconnect simultaneously. Furthermore, it's boilerplate code that can be replaced by a standard, well-tested library.

**Proposed Solution:**

1. Add the `Polly` NuGet package to the `Dyalog.Hmon.Client.Lib` project.
2. Refactor the `ConnectWithRetriesAsync` method in `ServerConnection.cs`.
3. Define a `Polly.Retry.AsyncRetryPolicy`. This policy will be configured to:
    - Handle specific exceptions (e.g., `SocketException`, `IOException`).
    - Use an exponential backoff strategy, matching the existing `BackoffMultiplier`.
    - Introduce jitter to desynchronize retries.
    - Respect the `InitialDelay` and `MaxDelay` from the existing `RetryPolicy` record.
4. Wrap the connection logic (`tcpClient.ConnectAsync`, etc.) inside a `policy.ExecuteAsync(...)` call.

**API Changes:**
None. This is a purely internal implementation detail.

**Impact and Risks:**
- **Positive:** Significantly improves the reliability of connections, reduces technical debt, and makes the retry logic more declarative and maintainable.
- **Negative:** Adds a new third-party dependency (`Polly`). This is a low risk as Polly is a standard, well-supported library in the .NET ecosystem.

**Alternatives Considered:**
- **Keep manual implementation:** Rejected because it is less robust and harder to maintain.
- **Write a more complex manual implementation:** Rejected as it would be reinventing the wheel.

---

#### **Feature Brief #2: Implement Typed Exceptions for Error Handling**

**Problem Statement:**
The `HmonOrchestrator` currently reports most errors through a generic `OnError` C# event or by throwing `InvalidOperationException`. This makes it difficult for a consumer to programmatically handle different types of failures. A consumer has to parse exception messages to understand what went wrong, which is brittle.

**Proposed Solution:**
Define a hierarchy of specific, public exception types in `Dyalog.Hmon.Client.Lib` and throw them from the orchestrator methods.

- `HmonException` (base class)
  - `SessionNotFoundException(Guid sessionId)`: Thrown when an API call is made with an invalid `SessionId`.
  - `HmonConnectionException(string message, Exception innerException)`: Base for connection-related errors.
    - `HmonHandshakeFailedException(string reason)`: Thrown when the DRP-T/HMON handshake fails.
  - `HmonCommandException(string message)`: Base for command-related errors.
    - `CommandTimeoutException(string commandName)`: Thrown when a command does not receive a response within a configured timeout.

The `OnError` event can be kept for logging/diagnostic purposes but should not be the primary mechanism for flow control.

**API Changes:**
- Methods like `GetFactsAsync`, `SubscribeAsync`, etc., will now be documented to throw these specific exceptions.
- The public API will include the new exception types.

**Impact and Risks:**
- **Positive:** Enables consumers to write robust, type-safe `try/catch` blocks. The API becomes more self-documenting about its failure modes.
- **Negative:** This is a behavioral breaking change for consumers who were relying solely on the `OnError` event. This should be communicated clearly in the release notes.

---

### **Architectural Decision Record (ADR)**

---

#### **ADR-001: Unified Event Stream for `HmonOrchestrator`**

**Status:** Proposed

**Context:**
The `HmonOrchestrator` class, which is the primary entry point to the client library, currently exposes two different models for consuming events:

1. A modern, pull-based asynchronous stream: `public IAsyncEnumerable<HmonEvent> Events { get; }`
2. A set of traditional, push-based C# events: `ClientConnected`, `ClientDisconnected`, `OnSessionUpdated`.

This dual model creates API confusion, forcing the consumer to subscribe to multiple sources to get a complete picture of the system's state. It also introduces the possibility of race conditions, as the ordering between events raised via C# `event` and events yielded from the `IAsyncEnumerable` is not guaranteed.

**Decision:**
We will refactor the `HmonOrchestrator` to use a **single, unified event stream** as the sole source of truth for all activities.

1. The C# events (`ClientConnected`, `ClientDisconnected`, `OnSessionUpdated`) will be marked as `[Obsolete]` and subsequently removed in the next major version.
2. New record types will be introduced to represent lifecycle events within the main stream:
    - `public record SessionConnectedEvent(Guid SessionId, string Host, int Port, string? FriendlyName) : HmonEvent(SessionId);`
    - `public record SessionDisconnectedEvent(Guid SessionId, string Reason) : HmonEvent(SessionId);`
3. The internal logic of the orchestrator will be modified to `Post` these new event types to the internal `Channel` instead of invoking the C# events.
4. Consumers will now be instructed to process all events, including connection and disconnection, from the single `orchestrator.Events` stream.

**Consequences:**
- **Positive:**
  - **Simplified API:** Consumers have a single, predictable place to get all information.
  - **Guaranteed Ordering:** All events are processed in the order they occurred.
  - **Reduced Complexity:** Removes the need for consumers to manage multiple event handlers and subscriptions.
  - **Eliminates Race Conditions:** State changes can be handled atomically within a single `await foreach` loop.

- **Negative:**
  - **Breaking Change:** This is a significant breaking change for any existing consumer of the library. It necessitates a major version bump (e.g., from v1.x to v2.0).
  - **Migration Effort:** Consumers will need to refactor their event handling logic to use the new `switch` pattern on the event stream instead of attaching to C# events. Clear documentation and migration guides will be essential.

This change aligns the library with modern, reactive programming patterns and ultimately leads to a more robust and easier-to-use API, despite the initial cost of the breaking change.

You are absolutely right. My apologies for omitting those. Here are the three remaining feature briefs, written in the same detailed format.

---

#### **Feature Brief #3: Implement a Fact Caching Policy with TTL**

**Problem Statement:**
The `HmonOrchestrator._factCache` provides a valuable performance enhancement by caching the latest facts from each session. However, it lacks an invalidation or expiration mechanism. If polling stops for a given session (either intentionally or due to a prolonged disconnection), the cache will hold onto its last known values indefinitely. The public `GetFact<T>()` method will then continue to serve this stale data to consumers, which can lead to incorrect monitoring dashboards and faulty application logic.

**Proposed Solution:**
We will implement a simple Time-To-Live (TTL) check on the cached facts to ensure consumers do not receive outdated information.

1.  **Configuration:** Add a new property to the `HmonOrchestratorOptions` record to make the TTL configurable.
    ```csharp
    // In HmonOrchestratorOptions.cs
    public record HmonOrchestratorOptions
    {
        // ... existing properties
        public TimeSpan FactCacheTTL { get; init; } = TimeSpan.FromMinutes(5);
    }
    ```
2.  **Read-Time Invalidation:** Modify the `GetFact` and `GetFactWithTimestamp` methods in `HmonOrchestrator.cs`. Before returning a cached fact, these methods will perform a check against the `LastUpdated` timestamp stored in the `FactCacheEntry`.
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

**API Changes:**
*   **Added:** A new configuration property `HmonOrchestratorOptions.FactCacheTTL`.
*   **Modified (Behavioral):** The `GetFact<T>()` and `GetFactWithTimestamp()` methods will now return `null` if the cached fact is older than the configured TTL. This is a subtle but important behavioral change that enhances correctness.

**Impact and Risks:**
*   **Positive:** Prevents consumers from acting on stale data, significantly improving the correctness and reliability of any application built on the library.
*   **Negative:** A minor behavioral breaking change. Consumers who assumed `GetFact<T>()` would always return a value after the first poll will now need to handle `null` responses for expired data. This is a positive change for correctness and should be highlighted in release notes.

**Alternatives Considered:**
*   **Background Scavenger Task:** A `Timer` could periodically scan the `_factCache` and remove expired items. This was rejected as being more complex and resource-intensive than the simple, on-demand check-on-read approach, which achieves the same goal with lower overhead.

---

#### **Feature Brief #4: Add Configurable, Per-Command Timeouts to the Orchestrator**

**Problem Statement:**
The public interaction methods on `HmonOrchestrator` (e.g., `GetFactsAsync`, `SubscribeAsync`) rely solely on a `CancellationToken` for timing out. If an interpreter becomes unresponsive in a way that doesn't close the TCP socket, a command sent without a CancellationToken (or with a long-lived one) can hang indefinitely, blocking the calling thread. The library should provide a built-in safety net against this scenario.

**Proposed Solution:**
We will introduce a default timeout at the orchestrator level and allow it to be overridden on a per-call basis.

1.  **Configuration:** Add a default timeout property to `HmonOrchestratorOptions`.
    ```csharp
    // In HmonOrchestratorOptions.cs
    public record HmonOrchestratorOptions
    {
        // ... existing properties
        public TimeSpan DefaultCommandTimeout { get; init; } = TimeSpan.FromSeconds(30);
    }
    ```
2.  **API Overloads:** Update the signatures of all asynchronous interaction methods to accept an optional `TimeSpan? timeout` parameter.
    ```csharp
    // Example in HmonOrchestrator.cs
    public Task<FactsResponse> GetFactsAsync(
        Guid sessionId,
        IEnumerable<FactType> facts,
        TimeSpan? timeout = null, // New parameter
        CancellationToken ct = default);
    ```3.  **Internal Implementation:** Internally, these methods will create a linked `CancellationTokenSource` that combines the user-provided `CancellationToken` with a new `CancellationTokenSource` set to the specified timeout (or the default).
    ```csharp
    // Example implementation detail
    var effectiveTimeout = timeout ?? _options.DefaultCommandTimeout;
    using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
    
    // Pass linkedCts.Token to SendCommandAsync
    var evt = await conn.SendCommandAsync<FactsReceivedEvent>("GetFacts", payload, linkedCts.Token);
    ```
    If the command times out, it will now throw an `OperationCanceledException` that can be caught and handled appropriately.

**API Changes:**
*   **Added:** A new configuration property `HmonOrchestratorOptions.DefaultCommandTimeout`.
*   **Added:** A new optional `TimeSpan? timeout` parameter to all relevant public methods: `GetFactsAsync`, `GetLastKnownStateAsync`, `PollFactsAsync`, `StopFactsPollingAsync`, `BumpFactsAsync`, `SubscribeAsync`, `ConnectRideAsync`, `DisconnectRideAsync`.

**Impact and Risks:**
*   **Positive:** Greatly improves the resilience and predictability of the client library. It prevents application threads from hanging on unresponsive network endpoints and provides a clear, configurable timeout mechanism.
*   **Negative:** None. This is a non-breaking, purely additive change to the API.

**Alternatives Considered:**
*   **Relying solely on `CancellationToken`:** This was rejected because it places the full burden of timeout management on every single consumer, making the library harder to use safely. Providing a sensible default is a better practice.

---

#### **Feature Brief #5: Refactor OTEL Logging to use Serilog Enrichers**

**Problem Statement:**
The `Dyalog.Hmon.OtelAdapter` project currently uses a custom static class, `ScopedLoggerExtensions`, to add contextual properties (like `session.id`, `host.name`) to its log records. While this works, it is not the idiomatic or most effective way to handle contextual logging with Serilog. It requires developers to remember to use the special `Log...WithContext` methods and manually pass a dictionary of properties for every log call.

**Proposed Solution:**
We will refactor the logging implementation to use Serilog's built-in context enrichment features, which are more powerful, maintainable, and aligned with best practices.

1.  **Remove Custom Extension:** The `Dyalog.Hmon.OtelAdapter/LoggerExtensions.cs` file will be deleted.
2.  **Configure LogContext:** In `Program.cs`, the Serilog `LoggerConfiguration` will be updated to include the context enricher.
    ```csharp
    // In Program.cs
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Is(...)
        .Enrich.FromLogContext() // Add this line
        .WriteTo.Console(...)
        .CreateLogger();
    ```
3.  **Use `LogContext` for Scoping:** In `AdapterService.cs`, wrap the event processing logic for a single HMON event in a `using` block that pushes all relevant session properties into the `LogContext`.
    ```csharp
    // In AdapterService.cs, inside ProcessEventsAsync loop
    var sessionTags = ... // Get the tags for the session
    using (LogContext.PushProperty("SessionId", hmonEvent.SessionId))
    using (LogContext.Push(...)) // Push other relevant properties
    {
        // All logging calls within this block will be automatically enriched
        switch (hmonEvent)
        {
            case FactsReceivedEvent e:
                 // Now just call the standard logger method
                 _otelLogger.LogInformation("Processing {FactCount} facts", e.Facts.Facts.Count());
                 break;
            // ... other cases
        }
    }
    ```
    All calls to the old `_otelLogger.LogInformationWithContext(...)` will be replaced with standard `_otelLogger.LogInformation(...)` calls.

**API Changes:**
None. This is a purely internal refactoring of the adapter's implementation.

**Impact and Risks:**
*   **Positive:** Aligns the project with standard Serilog practices. Logging code becomes cleaner, as the enrichment is handled automatically by the context. It removes the need for the custom extension class, reducing bespoke code and maintenance.
*   **Negative:** None.

**Alternatives Considered:**
*   **Continue using the custom extension methods:** Rejected because it's non-standard, more verbose for developers, and less flexible than using Serilog's built-in context features.