# System Patterns: HMON-to-OTEL Adapter

_Last reviewed: 2025-07-16 09:38 CEST_

## Architecture
- Adapter service runs as a standalone .NET process.
- Connects to HMON event/metric streams via TCP/WebSocket.
- Translates HMON protocol data to OpenTelemetry metrics and traces.
- Forwards OTEL data to configured OTEL collector endpoints.

## Key Technical Decisions
- Use Serilog for structured logging and diagnostics.
- Use SpectreConsole for CLI interactions and configuration.
- Use SQLite for optional local persistence or buffering.
- Modular mapping layer for HMON-to-OTEL translation.
- Resilient connection management and error handling.

## Design Patterns
- Adapter pattern for protocol translation.
- Observer pattern for event stream handling.
- Strategy pattern for configurable mapping/filtering.
- Dependency injection for extensibility and testability.

## Component Relationships
- HMON connection manager → Event/metric stream processor → OTEL mapping layer → OTEL exporter.
- Logging and diagnostics available at each stage.
