# Tech Context: HMON-to-OTEL Adapter

_Last reviewed: 2025-07-16 09:39 CEST_

## Technologies Used
- .NET 9.0, C# 13
- Serilog for logging
- SpectreConsole for CLI
- SQLite for optional local persistence
- OpenTelemetry .NET SDK for metrics/traces
- TCP/WebSocket for HMON protocol connectivity

## Development Setup
- Visual Studio Code or Visual Studio
- Standard .NET project structure
- Unit and integration tests via xUnit
- CI/CD pipeline (GitHub Actions recommended)

## Technical Constraints
- Must support real-time event forwarding with minimal latency
- Configurable mapping/filtering of events and metrics
- Robust error handling and diagnostics
- Easy deployment as a standalone service

## Dependencies
- Serilog
- Spectre.Console
- Microsoft.Data.Sqlite
- OpenTelemetry

## Tool Usage Patterns
- Logging: Serilog with structured output
- CLI: SpectreConsole for configuration and diagnostics
- Persistence: SQLite for buffering or local storage
- Protocol translation: Modular mapping layer for HMON-to-OTEL
- All API usage for HMON and OTEL mapping must be verified against the codebase, the context files, the PRDs, the available documentation and the RFCs. No guessing or hallucination of method names or behaviors.
- external APIs, such as the opentelemetry .NET sdk should be checked using context7 in case of doubts: no guessing or hallucination of method names or behaviors.
