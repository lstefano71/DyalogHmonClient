# Feature Brief #1: Introduce Polly for Resilient Connection Retries

## Problem Statement

The current connection retry logic in `Dyalog.Hmon.Client.Lib/ServerConnection.cs` is implemented with a manual `Task.Delay` and a backoff multiplier. This is a common pattern that is simple but lacks robustness. It does not include jitter, which can lead to synchronized retries in a thundering herd problem if multiple clients disconnect simultaneously. Furthermore, it's boilerplate code that can be replaced by a standard, well-tested library.

## Proposed Solution

1. Add the `Polly` NuGet package to the `Dyalog.Hmon.Client.Lib` project.
2. Refactor the `ConnectWithRetriesAsync` method in `ServerConnection.cs`.
3. Define a `Polly.Retry.AsyncRetryPolicy`. This policy will be configured to:
    - Handle specific exceptions (e.g., `SocketException`, `IOException`).
    - Use an exponential backoff strategy, matching the existing `BackoffMultiplier`.
    - Introduce jitter to desynchronize retries.
    - Respect the `InitialDelay` and `MaxDelay` from the existing `RetryPolicy` record.
4. Wrap the connection logic (`tcpClient.ConnectAsync`, etc.) inside a `policy.ExecuteAsync(...)` call.

## API Changes

None. This is a purely internal implementation detail.

## Impact and Risks

- **Positive:** Significantly improves the reliability of connections, reduces technical debt, and makes the retry logic more declarative and maintainable.
- **Negative:** Adds a new third-party dependency (`Polly`). This is a low risk as Polly is a standard, well-supported library in the .NET ecosystem.

## Alternatives Considered

- **Keep manual implementation:** Rejected because it is less robust and harder to maintain.
- **Write a more complex manual implementation:** Rejected as it would be reinventing the wheel.
