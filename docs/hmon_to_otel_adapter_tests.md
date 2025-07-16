# HMON to OpenTelemetry Adapter: Test Strategy

This document outlines the unit and integration testing approach for the Dyalog.Hmon.OtelAdapter project.

## Unit Testing

Unit tests should cover:
- Fact-to-metric mapping logic (HostFact, WorkspaceFact, AccountInformationFact, etc.)
- Notification-to-log mapping logic (UntrappedSignal, TrappedSignal, WorkspaceResize, UserMessage)
- Resource enrichment logic for per-session MeterProvider
- Error handling and lifecycle event logging

Recommended framework: xUnit or NUnit.

## Integration Testing

Integration tests should verify:
- End-to-end ingestion of HMON events and facts from a mock HMON server
- Correct export of metrics and logs to an in-memory or test OTel collector
- Graceful shutdown and resource disposal

Recommended approach:
- Use Dyalog.Hmon.Client.Tests.MockHmonServer to simulate interpreter events
- Use OpenTelemetry in-memory exporters for metrics/logs

## Example Test Cases

- Mapping a HostFact to expected OTel resource attributes and metrics
- Mapping a WorkspaceFact to expected metrics with correct tags
- Mapping a NotificationReceivedEvent (UntrappedSignal) to a log record with enriched attributes
- Verifying that AdapterService disposes resources on shutdown
- Verifying that Ctrl+C triggers graceful shutdown

## Directory Structure

- `Dyalog.Hmon.OtelAdapter.Tests/` — Test project for the adapter
- `Dyalog.Hmon.Client.Tests/MockHmonServer.cs` — Mock server for integration tests

## Getting Started

1. Create the test project:
   ```
   dotnet new xunit -n Dyalog.Hmon.OtelAdapter.Tests
   dotnet add Dyalog.Hmon.OtelAdapter.Tests reference Dyalog.Hmon.OtelAdapter
   dotnet add Dyalog.Hmon.OtelAdapter.Tests reference Dyalog.Hmon.Client.Lib
   ```

2. Add test files for each major area (mapping, logging, lifecycle, integration).

3. Run tests:
   ```
   dotnet test Dyalog.Hmon.OtelAdapter.Tests
   ```

## References

- [OpenTelemetry .NET Testing Guide](https://opentelemetry.io/docs/instrumentation/net/testing/)
- [Dyalog.Hmon.Client.Tests/MockHmonServer.cs](../Dyalog.Hmon.Client.Tests/MockHmonServer.cs)
