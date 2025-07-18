# Active Context: HMON-to-OTEL Adapter

_Last reviewed: 2025-07-18 17:29 CEST_

## Current Focus
- Project focus is on hmon-to-otel-adapter and core client library.
- Unified event stream is now the sole API for HmonOrchestrator; all obsolete event-based APIs have been removed.
- Documentation and API guide updated to reflect the new event stream model.
- Preferences for logging (Serilog), CLI (SpectreConsole), and embedded database (SQLite) remain unchanged.

## Next Steps
- Continue adapter logic for HMON-to-OTEL translation.
- Ensure all client code and integrations use the unified event stream.
- Thoroughly test handshake, interaction API, and connection reliability, including error/failure scenarios.
- Write and run unit/integration tests for new event stream logic.
- Review and update documentation as needed.

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
- Feature Brief #1: Polly-based retry logic for connection management implemented in ServerConnection.cs.
- docs/improvements/todo.md updated to mark Feature Brief #1 as complete.
- Memory bank and TODOs reviewed and updated.
- Project context and requirements confirmed.
