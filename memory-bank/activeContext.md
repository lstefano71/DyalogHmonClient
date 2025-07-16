# Active Context: HMON-to-OTEL Adapter

_Last reviewed: 2025-07-16 09:37 CEST_

## Current Focus
- Project focus switched to hmon-to-otel-adapter.
- Adapter design and implementation to translate HMON events and metrics to OpenTelemetry format.
- Review and follow requirements in docs/hmon-to-otel-adapter-PRD.md.
- All architectural and implementation decisions now reference the adapter PRD and TODO-hmonadapter.md.
- Preferences for logging (Serilog), CLI (SpectreConsole), and embedded database (SQLite) remain unchanged.

## Next Steps
- Thoroughly test handshake, interaction API, and connection reliability, including error/failure scenarios.
- Add comprehensive XML documentation.
- Write and run unit/integration tests.
- Update documentation as needed.

## Previous Focus
- HmonOrchestrator interaction API (GetFactsAsync, GetLastKnownStateAsync, PollFactsAsync, StopFactsPollingAsync, BumpFactsAsync, SubscribeAsync, ConnectRideAsync, DisconnectRideAsync) implemented.
- Handshake protocol and connection validation now implemented as per RFCs.
- Finalizing reliability (testing retry logic, robust error handling, state management).
- Sample client and documentation now fully refactored to use streamlined SessionMonitorBuilder API.
- Adding comprehensive XML documentation to all public APIs.
- Writing unit and integration tests for core logic and orchestrator API.

## Recent Changes
- Unit tests implemented for HmonOrchestrator, HmonConnection, and ServerConnection (construction, disposal, error handling).
- Integration test implemented for end-to-end handshake using MockHmonServer.
- XML documentation added to all public APIs in HmonOrchestrator, ServerConnection, and HmonConnection.
- Serilog logging integrated in HmonOrchestrator for connection lifecycle, handshake failures, and disposal.
- Serilog logging integrated in ServerConnection and HmonConnection for connection attempts, failures, and error diagnostics.
- All HmonOrchestrator interaction API methods implemented, using HmonConnection and matching HMON protocol.
- Handshake logic (RFC 001/002) implemented in HmonConnection and invoked by HmonOrchestrator before consumer events.
- Failed handshakes now result in immediate connection cleanup and are not exposed to consumers.
- Core infrastructure and project setup completed (solution, library, console, and test projects).
- Core concepts, configuration models, enumerations, lifecycle event arguments, unified data event stream models, and HMON message payload models implemented.
- JSON serialization configured with System.Text.Json and source generation.
- HmonOrchestrator constructor, connection management (listener, AddServer, RemoveServerAsync), event handling, and disposal implemented.
- Consumer workflow example implemented in the console project.
- Sample client and documentation refactored to use SessionMonitorBuilder for session management and state updates.
- Sample client documentation updated at `docs/sample-client.md` to describe builder-based workflow.
- Documentation reviewed and updated.
