# **Product Requirements Document: HMON to OpenTelemetry Adapter**

| **Version** | **Date**      | **Author** | **Status** |
| :---------- | :------------ | :--------- | :--------- |
| 1.1         | July 15, 2025 | Gemini     | Final      |

## 1. Introduction & Vision

### 1.1. Vision

To seamlessly integrate Dyalog APL application monitoring into the broader, standardized cloud-native observability ecosystem. This adapter will act as a vital bridge, translating the rich, specific data from Dyalog's Health Monitor (HMON) protocol into OpenTelemetry (OTel) signals. This will empower developers and operators to use industry-standard tools like Prometheus, Grafana, Jaeger, and Datadog for comprehensive monitoring, logging, and diagnostics of their APL applications alongside the rest of their infrastructure.

### 1.2. Purpose

This document specifies the requirements for the **HMON to OpenTelemetry Adapter**, a .NET console application. The adapter will leverage the existing `Dyalog.Hmon.Client` library to connect to multiple Dyalog interpreters, consume HMON data streams, and export them as standards-compliant OpenTelemetry metrics and logs. Its behavior will be entirely configurable through a combination of a configuration file and command-line arguments, ensuring flexibility for various deployment scenarios.

## 2. Goals and Objectives

* **Primary Goal:** To provide a robust, configuration-driven, and performant service that converts HMON data into OTLP (OpenTelemetry Protocol) format for consumption by any OTel-compatible backend.
* **Key Objectives:**
* **Comprehensive Data Mapping:** Ensure a complete and logical mapping of HMON `Facts` to OTel `Metrics`, and HMON `Notifications` and connection lifecycle events to OTel `Logs`, enriching them with all available contextual attributes.
* **Flexible Connectivity:** Support simultaneous connections to multiple Dyalog interpreters in both `SERVE` mode (adapter initiates connection) and `POLL` mode (adapter listens for connections).
* **Robust Configuration:** Allow all operational parameters, including HMON targets and the OTel collector endpoint, to be configured via a JSON file, with overrides from command-line arguments.
* **Operational Transparency:** Provide clear, structured console logging (via Serilog) to give operators insight into the adapter's status, connection health, and data flow.
* **Resilience:** Gracefully handle and report transient connection issues with Dyalog interpreters as distinct OTel log entries, leveraging the underlying reliability features of the `Dyalog.Hmon.Client` library.

## 3. Core Features & Functionality

### 3.1. Configuration System

The adapter's behavior MUST be controlled by a configuration system with a clear order of precedence:

1. Command-Line Arguments
2. Environment Variables
3. Configuration File (`config.json`)

#### 3.1.1. Configuration File (`config.json`)

A JSON file that defines the adapter's setup.

* **Schema:**

    ```json
    {
      "hmonServers": [
        {
          "name": "WebAppServer_1",
          "host": "10.0.1.50",
          "port": 4502
        },
        {
          "name": "BatchProcessor_A",
          "host": "apl-batch-a.internal",
          "port": 4502
        }
      ],
      "pollListener": {
        "ip": "0.0.0.0",
        "port": 4501
      },
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

* **Details:**
* `hmonServers`: (Optional) An array of HMON server objects for the adapter to connect to (`SERVE` mode).
    *`name`: A friendly, unique name for the server, used in OTel resource attributes.
    *   `host`, `port`: Connection details for the HMON server.
* `pollListener`: (Optional) An object specifying the IP and port for the adapter to listen on for incoming connections from interpreters (`POLL` mode). At least one of `hmonServers` or `pollListener` MUST be present.
* `openTelemetryExporter`: (Required) Configuration for the OTLP exporter.
    *`endpoint`: The URL of the OpenTelemetry Collector endpoint.
    *   `protocol`: (Optional) The OTLP protocol to use. Can be `Grpc` or `HttpProto`. Defaults to `Grpc`.
* `monitoring`: (Optional) Defines the monitoring strategy.
    *`serviceName`: (Optional) The service name for the adapter itself in OTel. Defaults to `DyalogHmonAdapter`.
    *   `pollIntervalSeconds`: (Optional) The interval in seconds at which to poll for facts. Defaults to `15`.
    *   `subscribedEvents`: (Optional) An array of event names to subscribe to on each connection. Defaults to all available events.
* `logging`: (Optional) Configuration for the adapter's own console logging.
    *   `logLevel`: (Optional) The minimum level for console logs. Can be `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`. Defaults to `Information`.

#### 3.1.2. Command-Line Arguments

Arguments to override file-based settings.

* `--config <path>`: Path to the `config.json` file.
* `--otel-endpoint <url>`: Overrides `openTelemetryExporter.endpoint`.
* `--otel-protocol <protocol>`: Overrides `openTelemetryExporter.protocol`.
* `--log-level <level>`: Overrides `logging.logLevel`.
* `--service-name <name>`: Overrides `monitoring.serviceName`.

### 3.2. HMON Connectivity

The adapter MUST use the `Dyalog.Hmon.Client.HmonOrchestrator` to manage all APL interpreter connections.

* On startup, it will iterate through the `hmonServers` array and call `orchestrator.AddServer()` for each entry.
* If `pollListener` is configured, it will call `orchestrator.StartListenerAsync()`.
* For every successfully connected client, it will automatically call `SubscribeAsync` and `PollFactsAsync` using the intervals and event lists from the `monitoring` configuration section.

### 3.3. HMON to OpenTelemetry Mapping

This is the core translation logic of the adapter.

#### 3.3.1. Enriched Resource Attributes

For each unique HMON session, a distinct OTel `Resource` MUST be created. This resource will be attached to all metrics and logs originating from that session. The attributes for the resource MUST be populated from the `HostFact` and `AccountInformationFact`.

| OTel Resource Attribute         | HMON Source (`Fact`)            | Example                        | Notes                                             |
| ------------------------------- | ------------------------------- | ------------------------------ | ------------------------------------------------- |
| `service.name`                  | `hmonServers[i].name` / `Machine.Desc` | `WebAppServer_1`               | Use friendly name from config; fallback to `Desc` |
| `service.instance.id`           | `Interpreter.SessionUUID`       | `a1b2c3d4-e5f6-...`            | Uniquely identifies the interpreter session.      |
| `service.version`               | `Interpreter.Version`           | `19.0.49500`                   | The Dyalog APL version.                           |
| `host.name`                     | `Machine.Name`                  | `apl-prod-server-01`           | Hostname of the machine running APL.              |
| `process.pid`                   | `Machine.PID`                   | `12345`                        | Process ID of the interpreter.                    |
| `process.owner`                 | `Machine.User`                  | `svc_apl`                      | The user account running the process.             |
| `enduser.id`                    | `AccountInformation.UserIdentification` | `JSMITH`              | The `⎕AI` user identification.                  |
| `dyalog.session.id`             | *(from Orchestrator)*           | `GUID`                         | The adapter's internal stable SessionId.          |
| `dyalog.hmon.access_level`      | `Machine.AccessLevel`           | `2`                            | HMON connection access level.                     |
| `dyalog.interpreter.bitwidth`   | `Interpreter.BitWidth`          | `64`                           |                                                   |
| `dyalog.interpreter.is_unicode` | `Interpreter.IsUnicode`         | `true`                         |                                                   |
| `dyalog.interpreter.is_runtime` | `Interpreter.IsRuntime`         | `false`                        |                                                   |
| `dyalog.ride.listening`         | `RIDE.Listening`                | `true`                         | Is the interpreter listening for RIDE connections?|
| `dyalog.ride.address`           | `RIDE.Address`                  | `127.0.0.1`                    | RIDE listener address, if active.                 |
| `dyalog.conga.version`          | `CommsLayer.Version`            | `5.0.16`                       | Version of the Conga communications layer.        |

#### 3.3.2. Enriched Metrics Mapping

HMON `Facts` received in `FactsReceivedEvent` MUST be translated into OTel Metrics.

| OTel Metric Name                       | Type    | Unit            | Attributes / Dimensions | HMON Source (`Fact`)                         |
| -------------------------------------- | ------- | --------------- | ----------------------- | -------------------------------------------- |
| `dyalog.cpu.time`                      | Counter | `s`             |                         | `AccountInformationFact.ComputeTime`         |
| `dyalog.workspace.memory.used`         | Gauge   | `By` (Bytes)    | `wsid`                  | `WorkspaceFact.Used`                         |
| `dyalog.workspace.memory.available`    | Gauge   | `By`            | `wsid`                  | `WorkspaceFact.Available`                    |
| `dyalog.workspace.memory.allocation`   | Gauge   | `By`            | `wsid`                  | `WorkspaceFact.Allocation`                   |
| `dyalog.workspace.memory.allocation_hwm`| Gauge   | `By`            | `wsid`                  | `WorkspaceFact.AllocationHWM`                |
| `dyalog.workspace.compactions`         | Counter | `{compactions}` | `wsid`                  | `WorkspaceFact.Compactions`                  |
| `dyalog.workspace.gc.collections`      | Counter | `{collections}` | `wsid`                  | `WorkspaceFact.GarbageCollections`           |
| `dyalog.workspace.pockets.garbage`     | Gauge   | `{pockets}`     | `wsid`                  | `WorkspaceFact.GarbagePockets`               |
| `dyalog.workspace.pockets.free`        | Gauge   | `{pockets}`     | `wsid`                  | `WorkspaceFact.FreePockets`                  |
| `dyalog.workspace.pockets.used`        | Gauge   | `{pockets}`     | `wsid`                  | `WorkspaceFact.UsedPockets`                  |
| `dyalog.threads.count`                 | Gauge   | `{threads}`     |                         | `ThreadCountFact.Total`                      |
| `dyalog.threads.suspended.count`       | Gauge   | `{threads}`     |                         | `ThreadCountFact.Suspended`                  |

#### 3.3.3. Enriched Logs Mapping

HMON `Notifications`, `UserMessage` events, and adapter-generated lifecycle events MUST be translated into OTel Log Records.

| Event Source                 | Severity  | Body (Example)                                        | Key Attributes                                                                                                                                                                                                                                                            |
| ---------------------------- | --------- | ----------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **`UntrappedSignal`**        | `ERROR`   | `Untrapped Signal '{DMX.Message}' occurred in thread {Tid}` | `event.name`, `thread.id`, `exception.message`, `exception.stacktrace` (from formatted `Stack` array), `dyalog.dmx.info` (JSON string of the `DMX` object), `dyalog.thread.info` (JSON string of the full `ThreadInfo` object, fetched on-demand for enrichment)             |
| **`TrappedSignal`**          | `WARN`    | `Trapped Signal '{DMX.Message}' occurred in thread {Tid}`   | `event.name`, `thread.id`, `exception.message`, `exception.stacktrace`, `dyalog.dmx.info`, `dyalog.thread.info`                                                                                                                                                           |
| **`WorkspaceResize`**        | `INFO`    | `Workspace resized to {Size} bytes`                   | `event.name`, `dyalog.workspace.size` (from `Notification.Size`)                                                                                                                                                                                                          |
| **`UserMessage`**            | `INFO`    | `User message received`                               | `event.name`, `dyalog.usermsg.uid` (from `Message.UID`), `dyalog.usermsg.body` (JSON string of the `Message.Message` payload)                                                                                                                                                 |
| **Client Disconnected**      | `ERROR`   | `HMON client disconnected: {Reason}`                    | `event.name`: "ClientDisconnected", `net.peer.name` (Host), `net.peer.port` (Port), `error.message` (Reason)                                                                                                                                                               |
| **Connection Failed**        | `ERROR`   | `Failed to connect to HMON server {Host}:{Port}`        | `event.name`: "ConnectionFailed", `net.peer.name` (Host), `net.peer.port` (Port), `error.message` (Exception message)                                                                                                                                                         |
| **`LastKnownStateResponse`** | `DEBUG`   | `Last known state snapshot`                           | `event.name`, `dyalog.lks.timestamp`, `dyalog.lks.activity.code`, `dyalog.lks.location.function`, `dyalog.lks.location.line`, `dyalog.lks.wsfull.timestamp`                                                                                                                         |

## 4. Technical Stack

* **Language/Framework:** C# 13 / .NET 9.0
* **Core Library:** `Dyalog.Hmon.Client.Lib`
* **OpenTelemetry:**
* `OpenTelemetry`
* `OpenTelemetry.SDK`
* `OpenTelemetry.Exporter.Otlp`
* **Logging:** `Serilog` and `Serilog.Sinks.Console` for the adapter's own logging.

## 5. Non-Functional Requirements

* **Performance:** The adapter must have low CPU and memory overhead. It should efficiently handle data from dozens of concurrent HMON sessions. Asynchronous processing must be used throughout.
* **Reliability:** The adapter must be a long-running service. It must remain operational even if some of its target HMON servers are unavailable, and it must leverage the retry logic in `Dyalog.Hmon.Client` to re-establish connections.
* **Usability:** Configuration should be straightforward, and console output must be clear and provide actionable information about the adapter's health and the status of its connections.

## 6. Out of Scope

* **GUI:** This is a headless console application only.
* **Data Persistence:** The adapter is a stateless bridge; it does not store HMON or OTel data.
* **OTel Collector:** The project does not include the deployment or configuration of an OTel Collector, only the client-side exporting to one.
* **Trace Generation:** The adapter will not generate OTel traces. Its scope is limited to metrics and logs.
