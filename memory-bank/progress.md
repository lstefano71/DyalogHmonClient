# Progress: HMON-to-OTEL Adapter

_Last reviewed: 2025-07-16 09:39 CEST_

## What works
- Project focus switched to hmon-to-otel-adapter.
- Memory bank, instructions, and TODO updated for adapter context.
- Architectural, product, and technical context established.
- .NET console project scaffolded.
- Core dependencies (Serilog, Spectre.Console, Microsoft.Data.Sqlite, OpenTelemetry) installed.
- Project reference to Dyalog.Hmon.Client.Lib added.

## What's left to build
- Initial structure: set up Generic Host and AdapterService class. (complete)
- Configuration models and loading logic (complete)
- Console logging (complete)
- HMON client instantiation (complete)
- TelemetryFactory, ResourceBuilder, MeterProvider setup (complete)
- OTLP exporter for metrics configured (complete)
- Per-session MeterProvider and resource enrichment implemented (complete, see AdapterService.cs)
- LoggerProvider setup removed; log export to OTLP should be configured via Serilog or Microsoft.Extensions.Logging with OpenTelemetry extensions (see techContext.md)
- HMON interpreter connection (complete)
- Main event processing loop (complete)
- Metric mapping (in progress)
  - HostFact, WorkspaceFact, AccountInformationFact, InterpreterInfo: mapped to OTEL metrics using verified APIs and property names. (complete)
  - Additional Fact types (e.g., WorkspaceFact extra metrics, CommsLayerInfo, RideInfo, etc.): mapping in progress.
  - For each Fact in the payload (in progress)
  - Look up the corresponding OTel metric details from the PRD's "Enriched Metrics" table (in progress)
  - Create or retrieve the OTel Instrument (Gauge, Counter, etc.) (in progress)
  - Create the TagList of attributes (e.g., add wsid for workspace metrics) (in progress)
  - Record the measurement using the instrument (in progress)
- Adapter implementation: HMON event/metric ingestion, OTEL mapping, and export.
- Log enrichment for NotificationReceivedEvent signals (UntrappedSignal/TrappedSignal) implemented: DMX/Stack/ThreadInfo fetched and included in log record.
- Log enrichment for WorkspaceResize and UserMessage events implemented per PRD.
- Adapter-generated lifecycle logs (ClientDisconnected, ConnectionFailed) now include all required OTel attributes.
- Configurable mapping/filtering logic.
- Error handling: main event loop is now wrapped in try-catch and logs any unhandled exceptions.
- Graceful shutdown: AdapterService implements IAsyncDisposable, disposes HmonOrchestrator and MeterProviders. Program.cs now listens for Ctrl+C and triggers host shutdown.
- Error handling, diagnostics, and logging.
- Unit and integration tests.
- Documentation and usage guide.

## Current status
- Requirements and goals reviewed (see docs/hmon-to-otel-adapter-PRD.md).
- Memory bank and instructions updated.
- Ready to begin implementation.

## Known issues
- No adapter code implemented yet.

## Evolution of project decisions
- Project scope and context updated to focus on HMON-to-OTEL Adapter as of 2025-07-16.
