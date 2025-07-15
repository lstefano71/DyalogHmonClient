# HMon Hub Sample Documentation

## Overview

The HMon Hub Sample (`Dyalog.Hmon.HubSample.Web`) is a configuration-driven .NET 9.0 console/web application that aggregates facts and events from multiple Dyalog APL HMON servers. It exposes both REST and WebSocket APIs for real-time monitoring and integration.

## Features

- Aggregates facts from multiple HMON servers in real time.
- Exposes REST API endpoints for querying current facts and status.
- Provides a WebSocket endpoint for real-time event and fact updates.
- Supports per-session event history and event subscription.
- Robust error handling and automatic reconnection.
- Configurable via JSON files, environment variables, or CLI arguments.
- Logging via Serilog.

## Running the Hub Sample

Build and run the project:

```
dotnet build
dotnet run --project Dyalog.Hmon.HubSample.Web
```

## Configuration

The application loads configuration from a JSON file (default: `config.json`), which can be overridden via the `--config` CLI argument or the `HMON_HUB_CONFIG` environment variable.

### Example config.json

```json
{
  "hmonServers": [
    { "name": "MainServer", "host": "127.0.0.1", "port": 4502 }
  ],
  "pollListener": {
    "ip": "0.0.0.0",
    "port": 4501
  },
  "api": {
    "ip": "0.0.0.0",
    "port": 8080
  },
  "logLevel": "Debug",
  "pollFacts": ["host", "threads"],
  "pollIntervalSeconds": 5,
  "eventSubscription": ["UntrappedSignal"],
  "eventHistorySize": 10,
  "autoShutdownSeconds": 600
}
```

### Configuration Keys

- `api`: **(required)** IP and port for the REST/WebSocket API (object: `{ "ip": string, "port": int }`)
- `hmonServers`: List of HMON server endpoints to connect to. Each entry: `{ "name": string, "host": string, "port": int }`
- `pollListener`: IP and port for the poll listener (object: `{ "ip": string, "port": int }`)
- At least one of `hmonServers` or `pollListener` must be present.
- `logLevel`: Logging verbosity (e.g., "Debug", "Information")
- `pollFacts`: List of fact names to poll from servers (default: `["host", "threads"]`)
- `pollIntervalSeconds`: Polling interval in seconds (default: `5`)
- `eventSubscription`: List of event names to subscribe to (default: `["UntrappedSignal"]`)
- `eventHistorySize`: Number of events to retain per session (default: `10`)
- `autoShutdownSeconds`: Optional; if set, the hub will auto-shutdown after this many seconds of inactivity

### Configuration Precedence

1. `--config path/to/config.json` (CLI argument)
2. `HMON_HUB_CONFIG` (environment variable)
3. `config.json` in the current or application directory

If a key is missing, defaults are applied as described above.

## REST API Endpoints

- `GET /facts`  
  Returns the latest aggregated facts from all connected HMON servers.

- `GET /status`  
  Returns the current connection and session status for all managed servers.

## WebSocket Endpoint

- `GET /ws`  
  Clients can subscribe to this endpoint to receive real-time event and fact updates as JSON messages.

## Example Usage

1. Start the hub sample.
2. Connect one or more Dyalog APL servers (with HMON support) to the configured endpoints.
3. Query `/facts` or `/status` for current state, or connect to `/ws` for live updates.

## Logging

All connection lifecycle events, errors, and diagnostics are logged using Serilog. Logging output and verbosity can be configured in the appsettings files.

## Requirements

- .NET 9.0 SDK or later
- One or more Dyalog APL servers with HMON protocol support
