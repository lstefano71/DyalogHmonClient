# Issue #4: Bug: Polling and subscription settings are hardcoded in the adapter

**Labels:** `bug`, `config`, `otel-adapter`

## Problem

The `AdapterService` hardcodes the list of facts to poll and events to subscribe to when a new client connects. This ignores the `monitoring.pollIntervalSeconds` and `monitoring.subscribedEvents` sections defined in the PRD for the `config.json` file.

## Impact

The adapter is not flexible. Users cannot tailor the data collection to their needs (e.g., reducing polling frequency or subscribing only to critical events) without modifying the source code.

## Proposed Solution

1. In `AdapterService.ExecuteAsync`, read the polling interval and subscribed event names from the `_adapterConfig` object.
2. Convert the string names from the configuration into their corresponding `FactType` and `SubscriptionEvent` enum values. Provide a safe default if the configuration is missing.
3. Use these configurable lists when calling `_orchestrator.PollFactsAsync(...)` and `_orchestrator.SubscribeAsync(...)`.

## Acceptance Criteria

- The adapter must respect the `pollIntervalSeconds` and `subscribedEvents` arrays from `config.json`.
- If these settings are omitted from the config, the adapter should apply a sensible default (e.g., poll key facts every 15s, subscribe to all signals).
