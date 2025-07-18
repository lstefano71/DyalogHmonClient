# Active Context: HMON-to-OTEL Adapter

_Last reviewed: 2025-07-17 17:50 CEST_

## Current Focus
- Project focus is on hmon-to-otel-adapter.
- Adapter design and implementation to translate HMON events and metrics to OpenTelemetry format.
- Feature Brief #2 (typed, specific exceptions for error handling) implemented.
- Preferences for logging (Serilog), CLI (SpectreConsole), and embedded database (SQLite) remain unchanged.

## Next Steps
- Implement adapter logic for HMON-to-OTEL translation.
- Thoroughly test handshake, interaction API, and connection reliability, including error/failure scenarios.
- Write and run unit/integration tests for new error handling.
- Update documentation as needed.

## Previous Focus
- Feature Brief #4: Configurable, per-command timeouts for orchestrator implemented and documented.
- Feature Brief #2: Typed, specific exceptions for error handling implemented in Dyalog.Hmon.Client.Lib.
  - Added HmonException, SessionNotFoundException, HmonConnectionException, HmonHandshakeFailedException, CommandTimeoutException.
  - Refactored HmonOrchestrator, ServerConnection, HmonConnection to use new exceptions.
  - Updated XML documentation for public API methods.
- Feature Brief #1: Polly-based retry logic for connection management implemented in ServerConnection.cs.
- HmonOrchestrator interaction API implemented.
- Handshake protocol and connection validation implemented as per RFCs.
- Reliability and error handling tested.
- Sample client and documentation refactored to use streamlined SessionMonitorBuilder API.
- XML documentation added to all public APIs.
- Unit and integration tests for core logic and orchestrator API.

## Recent Changes
- Feature Brief #4: Configurable, per-command timeouts implemented in HmonOrchestrator and archived; documentation and todo list updated.
- Feature Brief #2: Typed, specific exceptions for error handling implemented and documented.
- docs/improvements/todo.md updated to mark Feature Brief #2 as complete.
- Memory bank and TODOs reviewed and updated.
- Project context and requirements confirmed.
