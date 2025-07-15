# Dyalog.Hmon.Client

A .NET 9.0 library and sample client for monitoring and interacting with Dyalog APL servers using the HMON protocol.

## Overview

This repository provides:
- `Dyalog.Hmon.Client.Lib`: A C# library implementing the HMON protocol for Dyalog APL server monitoring and orchestration.
- `Dyalog.Hmon.Client.Console`: A sample console application demonstrating live monitoring of Dyalog APL sessions.
- `Dyalog.Hmon.Client.Tests`: Unit and integration tests for the library.
- **HMon Hub Sample**: Configuration-driven console app with REST API and WebSocket endpoints for real-time fact/event updates and aggregation.

## Features

- Connection management and handshake protocol (see RFCs)
- Unified event stream for session facts and notifications
- Fact polling and subscription APIs
- Robust error handling and reconnection logic
- REST API endpoints (`/facts`, `/status`) for live data
- WebSocket endpoint (`/ws`) for real-time event/fact updates
- In-memory aggregation of facts
- Per-session event history and event subscription
- Logging with Serilog
- Live terminal dashboard (Spectre.Console)
- Comprehensive XML documentation

## Current Status

All major features are implemented:
- HMon orchestrator, REST API, WebSocket, fact aggregation, error handling, logging, and configuration are complete.
- Only unit/integration testing and documentation remain to be finalized.

## Getting Started

1. **Build the Solution**
   ```
   dotnet build
   ```

2. **Run the Sample Client**
   ```
   dotnet run --project Dyalog.Hmon.Client.Console
   ```
   The client listens for incoming Dyalog HMON server connections (default port: 8080).

3. **Run the HMon Hub Sample**
   ```
   dotnet run --project Dyalog.Hmon.HubSample.Web
   ```
   This exposes REST and WebSocket endpoints for real-time monitoring.

4. **Connect a Dyalog HMON Server**
   - Point your Dyalog APL server to the listening port.
   - Monitor live session facts and events in the terminal UI or via API/WebSocket.

## Documentation

- [Product Requirements Document (PRD)](docs/hmonclient-prd.md)
- [Sample Client Overview](docs/sample-client.md)
- [HMon Hub Sample Documentation](docs/hmon-hub-sample.md)
- [Usage Guide](docs/hmon-usage-guide.md)
- RFCs: See `RFCs/` directory for protocol specifications.

## Project Structure

- `Dyalog.Hmon.Client.Lib/` — Core library
- `Dyalog.Hmon.Client.Console/` — Sample client
- `Dyalog.Hmon.HubSample.Web/` — Hub sample (REST/WebSocket)
- `Dyalog.Hmon.Client.Tests/` — Tests
- `docs/` — Documentation
- `memory-bank/` — Project context and progress tracking
- `RFCs/` — Protocol specifications

## Requirements

- .NET 9.0 SDK or later
- Dyalog APL server with HMON support

## License

[MIT](LICENSE)
