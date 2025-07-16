# HMON to OpenTelemetry Adapter

This guide describes the **Dyalog.Hmon.OtelAdapter** project, which bridges Dyalog APL HMON monitoring to OpenTelemetry metrics and logs.

## Overview

The adapter connects to one or more Dyalog APL interpreters using the HMON protocol and exports facts, notifications, and lifecycle events as OpenTelemetry signals. This enables integration with cloud-native observability stacks (Prometheus, Grafana, Jaeger, Datadog, etc.).

## Features

- Per-session resource enrichment: Each interpreter session is mapped to a distinct OTel Resource with attributes from HostFact, AccountInformationFact, and connection metadata.
- Metric mapping: Workspace, account, and host facts are mapped to OTel metrics using verified APIs and property names.
- Log mapping: Notifications, user messages, and lifecycle events are mapped to OTel logs with full enrichment.
- Robust error handling and graceful shutdown.
- Console logging via Serilog.

## Getting Started

1. **Configure the Adapter**

   Edit `config.json` to specify HMON servers, OpenTelemetry exporter endpoint, polling interval, and other options.

2. **Build and Run**

   ```
   dotnet build Dyalog.Hmon.OtelAdapter
   dotnet run --project Dyalog.Hmon.OtelAdapter
   ```

3. **Connect Dyalog Interpreters**

   Point your Dyalog APL servers to the configured host/port(s).

4. **Monitor in OpenTelemetry**

   Exported metrics and logs will be sent to your configured OTel collector endpoint.

## Configuration

The adapter is configured via a single `config.json` file. All keys are PascalCase and must match the structure below.

### Example `config.json`

```json
{
  "ServiceName": "HMON-to-OTEL Adapter",
  "HmonServers": [
    {
      "Host": "127.0.0.1",
      "Port": 4502,
      "Name": "TestServer"
    }
  ],
  "OtelExporter": {
    "Endpoint": "http://localhost:4317",
    "Protocol": "Grpc",
    "ApiKey": null
  },
  "PollingIntervalMs": 5000,
  "LogLevel": "Information",
  "MeterName": "HMON",
  "PollListener": {
    "Ip": "0.0.0.0",
    "Port": 9000
  }
}
```

#### Configuration Fields

- **ServiceName** (string, required): Logical name for the adapter service.
- **HmonServers** (array, required): List of HMON server connections.
  - **Host** (string, required): Hostname or IP of the HMON server.
  - **Port** (int, required): Port number.
  - **Name** (string, optional): Friendly name for the server.
- **OtelExporter** (object, required): OpenTelemetry exporter configuration.
  - **Endpoint** (string, required): OTel collector endpoint (e.g., `http://localhost:4317`).
  - **Protocol** (string, optional): Export protocol (e.g., `Grpc`).
  - **ApiKey** (string, optional): API key for secured endpoints.
- **PollingIntervalMs** (int, optional): Fact polling interval in milliseconds (default: 5000).
- **LogLevel** (string, optional): Logging level (default: `Information`).
- **MeterName** (string, optional): Name for the OTel Meter (default: `HMON`).
- **PollListener** (object, optional): Enables polling listener if set.
  - **Ip** (string, optional): Listener IP (default: `0.0.0.0`).
  - **Port** (int, required): Listener port.

## Usage Examples

See [docs/hmon_to_otel_adapter_tests.md](hmon_to_otel_adapter_tests.md) for test strategy and sample test code.

```csharp
// Example: Injecting a HostFact via MockHmonServer in integration test
mockServer.EnqueueMessage(factsJson);
// AdapterService will process the fact and export metrics/logs accordingly
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
