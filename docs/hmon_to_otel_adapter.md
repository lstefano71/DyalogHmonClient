# HMON to OpenTelemetry Adapter

This guide describes the **Dyalog.Hmon.OtelAdapter** project, which bridges Dyalog APL HMON monitoring to OpenTelemetry metrics and logs.

## Overview

The adapter connects to one or more Dyalog APL interpreters using the HMON protocol and exports facts, notifications, and lifecycle events as OpenTelemetry signals. This enables integration with cloud-native observability stacks (Prometheus, Grafana, Jaeger, Datadog, etc.).

## Features

- Per-session resource enrichment: Each interpreter session is mapped to a distinct OTel Resource with attributes from HostFact, AccountInformationFact, and connection metadata.
- Metric mapping: Workspace, account, and host facts are mapped to OTel metrics using verified APIs and property names.
- Log mapping: Notifications, user messages, and lifecycle events are mapped to OTel logs with full enrichment.
- Configurable mapping/filtering logic.
- Robust error handling and graceful shutdown.
- Console logging via Serilog.

## Getting Started

1. **Configure the Adapter**

   Edit `config.json` to specify HMON servers, poll listener, OTel exporter endpoint, and monitoring options.

2. **Build and Run**

   ```
   dotnet build Dyalog.Hmon.OtelAdapter
   dotnet run --project Dyalog.Hmon.OtelAdapter
   ```

3. **Connect Dyalog Interpreters**

   Point your Dyalog APL servers to the configured host/port(s).

4. **Monitor in OpenTelemetry**

   Exported metrics and logs will be sent to your configured OTel collector endpoint.

## Usage Examples

### Example Adapter Configuration

```json
{
  "hmonServers": [
    { "name": "WebAppServer_1", "host": "10.0.1.50", "port": 4502 }
  ],
  "openTelemetryExporter": {
    "endpoint": "http://otel-collector:4317",
    "protocol": "Grpc"
  },
  "monitoring": {
    "serviceName": "DyalogHMONAdapter",
    "pollIntervalSeconds": 15,
    "subscribedEvents": ["UntrappedSignal", "WorkspaceResize", "UserMessage"]
  },
  "logging": {
    "logLevel": "Information"
  }
}
```

### Example Test Usage

See [docs/hmon_to_otel_adapter_tests.md](hmon_to_otel_adapter_tests.md) for test strategy and sample test code.

```csharp
// Example: Injecting a HostFact via MockHmonServer in integration test
mockServer.EnqueueMessage(factsJson);
// AdapterService will process the fact and export metrics/logs accordingly
```

## Configuration

See the [Product Requirements Document (PRD)](hmon-to-otel-adapter-PRD.md) for full details on configuration options and resource/metric/log mapping.

Example `config.json`:

```json
{
  "hmonServers": [
    { "name": "WebAppServer_1", "host": "10.0.1.50", "port": 4502 }
  ],
  "openTelemetryExporter": {
    "endpoint": "http://otel-collector:4317",
    "protocol": "Grpc"
  },
  "monitoring": {
    "serviceName": "DyalogHMONAdapter",
    "pollIntervalSeconds": 15,
    "subscribedEvents": ["UntrappedSignal", "WorkspaceResize", "UserMessage"]
  },
  "logging": {
    "logLevel": "Information"
  }
}
```

## Documentation

- [Adapter PRD](hmon-to-otel-adapter-PRD.md)
- [Usage Guide](hmon-usage-guide.md)
- [API Guide](hmonclient-api-guide.md)
- [RFCs](../RFCs/)

## Project Structure

- `Dyalog.Hmon.OtelAdapter/` — Adapter source code
- `docs/` — Documentation

## License

[MIT](../LICENSE)
