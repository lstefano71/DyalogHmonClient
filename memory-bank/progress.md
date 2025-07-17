# Progress: HMON-to-OTEL Adapter

_Last reviewed: 2025-07-17 17:51 CEST_

## What works
- Project focus is on hmon-to-otel-adapter.
- Memory bank and TODOs reviewed and confirmed up to date.
- Architectural, product, and technical context established.
- .NET console project scaffolded.
- Core dependencies (Serilog, Spectre.Console, Microsoft.Data.Sqlite, OpenTelemetry) installed.
- Project reference to Dyalog.Hmon.Client.Lib added.
- Feature Brief #2: Typed, specific exceptions for error handling implemented in Dyalog.Hmon.Client.Lib.
  - Added HmonException, SessionNotFoundException, HmonConnectionException, HmonHandshakeFailedException, CommandTimeoutException.
  - Refactored HmonOrchestrator, ServerConnection, HmonConnection to use new exceptions.
  - Updated XML documentation for public API methods.

## Completed Tasks

- Feature Brief #2: Typed, specific exceptions for error handling (2025-07-17).
- Feature Brief #1: Polly-based retry logic for resilient connection management implemented in ServerConnection.cs (2025-07-17).
- Initial structure: Generic Host and AdapterService class.
- Configuration models and loading logic.
- Console logging.
- HMON client instantiation.
- TelemetryFactory, ResourceBuilder, MeterProvider setup.
- OTLP exporter for metrics configured.
- Per-session MeterProvider and resource enrichment (see AdapterService.cs).
- LoggerProvider setup removed; log export to OTLP is via Serilog or Microsoft.Extensions.Logging with OpenTelemetry extensions.
- HMON interpreter connection.
- Main event processing loop.
- Metric mapping:
  - HostFact, WorkspaceFact, AccountInformationFact, InterpreterInfo mapped to OTEL metrics using verified APIs and property names.
  - Additional Fact types (WorkspaceFact extra metrics, CommsLayerInfo, RideInfo, etc.) mapped.
  - For each Fact in the payload, OTel metric details from the PRD's "Enriched Metrics" table are used.
  - OTel Instrument (Gauge, Counter, etc.) creation/retrieval.
  - TagList of attributes (e.g., wsid for workspace metrics).
  - Measurement recording using the instrument.
- Adapter implementation: HMON event/metric ingestion, OTEL mapping, and export.
- Log enrichment for NotificationReceivedEvent signals (UntrappedSignal/TrappedSignal): DMX/Stack/ThreadInfo fetched and included in log record.
- Log enrichment for WorkspaceResize and UserMessage events per PRD.
- Adapter-generated lifecycle logs (ClientDisconnected, ConnectionFailed) include all required OTel attributes.
- Configurable mapping/filtering logic.
- Error handling: main event loop wrapped in try-catch and logs any unhandled exceptions.
- Graceful shutdown: AdapterService implements IAsyncDisposable, disposes HmonOrchestrator and MeterProviders. Program.cs listens for Ctrl+C and triggers host shutdown.
- Documentation split: main README is now an introduction and index, with project-specific guides (including HMON to OTEL Adapter).
- Usage guides and PRDs for all major components.
- Diagnostics and logging.

## What's left to build

- Implement adapter logic for HMON-to-OTEL translation.
- Implement additional unit and integration tests (scaffolding, strategy, and first integration test implemented; MockHmonServer extended for robust testing. See [docs/hmon_to_otel_adapter_tests.md](../docs/hmon_to_otel_adapter_tests.md)).
- Project is now fully documented, including usage examples in [docs/hmon_to_otel_adapter.md](../docs/hmon_to_otel_adapter.md).

## Current status
- Feature Brief #2 (typed, specific exceptions for error handling) completed and documented.
- Feature Brief #1 (Polly integration for connection retries) completed and documented.
- Memory bank and TODOs reviewed and confirmed up to date.
- Next: Implement adapter logic for HMON-to-OTEL translation.
- Ready to begin implementation of adapter logic as per docs/hmon-to-otel-adapter-PRD.md and TODO-hmonadapter.md.

## Known issues
- No adapter code implemented yet.

## Evolution of project decisions
- Feature Brief #2: Adopted typed, specific exceptions for error handling in Dyalog.Hmon.Client.Lib as of 2025-07-17.
- Feature Brief #1: Adopted Polly for resilient connection retries in ServerConnection.cs as of 2025-07-17.
- Project scope and context updated to focus on HMON-to-OTEL Adapter as of 2025-07-16.
- Memory bank and TODOs reviewed and updated as of 2025-07-16 16:45 CEST.
