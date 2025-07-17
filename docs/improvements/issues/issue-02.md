# Issue #2: Bug: Memory leak and stale data from disconnected OTEL adapter sessions

**Labels:** `bug`, `high-priority`, `memory-leak`, `otel-adapter`

## Problem

The `AdapterService._sessionMetrics` dictionary, which stores metric values for each session, never removes entries. When a Dyalog interpreter disconnects, its `SessionId` and last known metric values remain in the dictionary indefinitely.

## Impact

1. **Memory Leak:** The service's memory usage will grow continuously in any environment where interpreter sessions are not permanent.
2. **Stale Data:** The adapter will continue to export the last-known metric values for disconnected sessions, polluting dashboards with incorrect, stale data.

## Proposed Solution

1. In `AdapterService.cs`, ensure the `ClientDisconnected` event handler is correctly wired up.
2. Within the event handler lambda, call `_sessionMetrics.TryRemove(args.SessionId.ToString(), out _)` to delete the entry for the disconnected session.

## Acceptance Criteria

- When a client disconnects, its corresponding entry must be removed from the `_sessionMetrics` dictionary.
- After a client disconnects, its metrics should no longer appear in the OTel export stream.
