# Active Context: HMON-to-OTEL Adapter

_Last reviewed: 2025-07-17 17:26 CEST_

## Current Focus
- Project focus is on hmon-to-otel-adapter.
- Adapter design and implementation to translate HMON events and metrics to OpenTelemetry format.
- Feature Brief #1 (Polly integration for resilient connection retries) completed.
- Next focus: Feature Brief #2 (typed, specific exceptions for error handling).
- Preferences for logging (Serilog), CLI (SpectreConsole), and embedded database (SQLite) remain unchanged.

## Next Steps
- Implement and use typed, specific exceptions for error handling (Feature Brief #2).
- Implement adapter logic for HMON-to-OTEL translation.
- Thoroughly test handshake, interaction API, and connection reliability, including error/failure scenarios.
- Add comprehensive XML documentation.
- Write and run unit/integration tests.
- Update documentation as needed.

## Previous Focus
- HmonOrchestrator interaction API implemented.
- Handshake protocol and connection validation implemented as per RFCs.
- Reliability and error handling tested.
- Sample client and documentation refactored to use streamlined SessionMonitorBuilder API.
- XML documentation added to all public APIs.
- Unit and integration tests for core logic and orchestrator API.

## Recent Changes
- Feature Brief #1: Polly-based retry logic for connection management implemented in ServerConnection.cs.
- docs/improvements/todo.md updated to mark Feature Brief #1 as complete.
- Memory bank and TODOs reviewed and updated.
- Project context and requirements confirmed.
