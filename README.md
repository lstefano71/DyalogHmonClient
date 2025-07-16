# Dyalog.Hmon.Client Repository

Welcome to the Dyalog.Hmon.Client monorepo, a .NET 9.0 suite for monitoring, orchestrating, and integrating Dyalog APL servers using the HMON protocol.

This repository contains several related projects and documentation for different use cases:

## 📦 Projects

- [`Dyalog.Hmon.Client.Lib`](docs/hmonclient-api-guide.md): Core C# library implementing the HMON protocol for Dyalog APL server monitoring and orchestration.
- [`Dyalog.Hmon.Client.Console`](docs/sample-client.md): Sample console application for live monitoring of Dyalog APL sessions.
- [`Dyalog.Hmon.HubSample.Web`](docs/hmon-hub-sample.md): Configuration-driven hub app with REST API and WebSocket endpoints for real-time fact/event updates and aggregation.
- [`Dyalog.Hmon.OtelAdapter`](docs/hmon_to_otel_adapter.md): **NEW!** Adapter for exporting HMON facts and events to OpenTelemetry metrics and logs (see below).
- [`Dyalog.Hmon.Client.Tests`](docs/hmonclient-api-guide.md): Unit and integration tests for the library.

## 📚 Documentation

- [Product Requirements Documents (PRDs)](docs/)
  - [HMON Client PRD](docs/original%20prd/hmonclient-prd.md)
  - [HMON Hub Sample PRD](docs/hmonhubsample-prd.md)
  - [HMON to OpenTelemetry Adapter PRD](docs/hmon-to-otel-adapter-PRD.md)
- [API Guide](docs/hmonclient-api-guide.md)
- [Sample Client Usage](docs/sample-client.md)
- [Hub Sample Usage](docs/hmon-hub-sample.md)
- [HMON to OpenTelemetry Adapter Guide](docs/hmon_to_otel_adapter.md)
- [General Usage Guide](docs/hmon-usage-guide.md)
- [RFCs](RFCs/): Protocol specifications

## 🆕 HMON to OpenTelemetry Adapter

See [`docs/hmon_to_otel_adapter.md`](docs/hmon_to_otel_adapter.md) for a full overview, configuration, and usage guide for the new adapter that bridges Dyalog HMON monitoring to OpenTelemetry metrics/logs.

## 🗂️ Project Structure

- `Dyalog.Hmon.Client.Lib/` — Core library
- `Dyalog.Hmon.Client.Console/` — Sample client
- `Dyalog.Hmon.HubSample.Web/` — Hub sample (REST/WebSocket)
- `Dyalog.Hmon.OtelAdapter/` — HMON to OTEL adapter
- `Dyalog.Hmon.Client.Tests/` — Tests
- `docs/` — Documentation
- `memory-bank/` — Project context and progress tracking
- `RFCs/` — Protocol specifications

## 🚀 Getting Started

See the individual project guides above for build and run instructions.

## 📝 License

[MIT](LICENSE)
