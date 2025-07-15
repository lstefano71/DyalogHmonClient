# Active Context: Dyalog.Hmon.Client

_Last reviewed: 2025-07-15 15:58 CEST_

## Current Focus
- HMon Hub Sample: HMon orchestrator integration, real-time fact aggregation, and WebSocket endpoint for real-time event/fact updates implemented.
- REST API endpoints (/facts, /status) now reflect live data (when HMon servers are available).
- Auto-shutdown, config loader, and Serilog logging are complete.
- Event subscription and per-session event history (configurable) implemented.
- Events are sent immediately through the websocket with timestamp and payload.
- Error handling improvements in progress.

## Next Steps
- Finalize error handling and connection status reporting.
- Update memory bank and TODO.md after WebSocket and error handling are implemented.

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

## Next Steps
- Thoroughly test handshake, interaction API, and connection reliability, including error/failure scenarios.
- Add XML documentation.
- Write and run unit/integration tests.

_Memory bank fully reviewed and updated as of 2025-07-15 17:31 CEST._
