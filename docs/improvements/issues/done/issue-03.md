# Issue #3: Bug: Incomplete log enrichment for `UntrappedSignal` and `TrappedSignal` events

**Labels:** `bug`, `logging`, `otel-adapter`

## Problem

The PRD for the OTEL adapter specifies that `UntrappedSignal` and `TrappedSignal` log records must be enriched with detailed thread, stack, and DMX information. This requires fetching the full `ThreadsFact` for the relevant thread *after* receiving the notification. The current implementation in `HandleNotificationReceivedEventAsync` fails to do this, logging only the minimal data available in the notification packet itself.

## Impact

Logs for critical errors are missing essential diagnostic information, significantly reducing their value for debugging.

## Proposed Solution

1. Modify `HandleNotificationReceivedEventAsync` in `AdapterService.cs`.
2. Inside the `case` for `"UntrappedSignal"` and `"TrappedSignal"`, after receiving the `notificationEvent`, use the `_orchestrator` to make a follow-up call: `await _orchestrator.GetFactsAsync(notificationEvent.SessionId, new[] { FactType.Threads }, stoppingToken)`.
3. Find the specific `ThreadInfo` object from the response that matches `notificationEvent.Notification.Tid`.
4. Add the full `Stack`, `DMX`, and `Exception` details from that `ThreadInfo` object to the log attributes before writing the log message.

## Acceptance Criteria

- A log generated for an `UntrappedSignal` must contain attributes for the full stack trace and DMX info.
- The implementation must handle cases where the follow-up `GetFactsAsync` call might fail.
