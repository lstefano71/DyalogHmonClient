# TODO List: Project Enhancements

This list is prioritized to tackle the most critical correctness issues first, followed by improvements that offer the most significant gains in robustness and maintainability.

## Phase 1: Critical Bug Fixes (Adapter Correctness)

- [x] **Issue #1:** Fix incorrect metric types in the OTEL adapter (Gauges to Counters).
- [x] **Issue #2:** Fix memory leak and stale data by removing disconnected sessions from the metrics dictionary.
- [x] **Issue #3:** Fix incomplete log enrichment for signal notifications.
- [x] **Issue #4:** Implement configuration for polling and subscription settings, removing hardcoded values.

## Phase 2: Robustness and API Improvements (Library & Adapter)

- [x] **Feature Brief #1:** Introduce the Polly library for resilient connection retries.
- [x] **Feature Brief #2:** Implement and use typed, specific exceptions for error handling.
- [ ] **Feature Brief #3:** Implement a fact caching policy with TTL to prevent stale data.

## Phase 3: Long-Term Architectural Refactoring (Library)

- [ ] **ADR #1:** Refactor the `HmonOrchestrator` to use a single, unified event stream, deprecating the dual-event model. *(Note: This is a breaking change and should be scheduled for a major version release, e.g., v2.0).*

## Phase 4: Code Quality and Developer Experience

- [x] **Feature Brief #4:** Add configurable, per-command timeouts to the orchestrator.
- [ ] **Feature Brief #5:** Refactor OTEL logging to use Serilog enrichers instead of custom extension methods.
