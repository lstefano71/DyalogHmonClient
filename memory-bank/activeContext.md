# Active Context: HMON-to-OTEL Adapter

_Last reviewed: 2025-07-16 16:45 CEST_

## Current Focus
- Project focus is on hmon-to-otel-adapter.
- Adapter design and implementation to translate HMON events and metrics to OpenTelemetry format.
- Memory bank and TODOs reviewed and confirmed up to date.
- Ready to begin implementation of adapter logic as per docs/hmon-to-otel-adapter-PRD.md and TODO-hmonadapter.md.
- Preferences for logging (Serilog), CLI (SpectreConsole), and embedded database (SQLite) remain unchanged.

## Next Steps
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
- Memory bank and TODOs reviewed and updated.
- Project context and requirements confirmed.
- Adapter implementation is the next priority.
