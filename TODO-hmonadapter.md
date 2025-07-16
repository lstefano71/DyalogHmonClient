# TODO: HMON to OpenTelemetry Adapter

This document outlines the tasks required to implement the HMON to OTel Adapter as specified in the [PRD](docs/hmon-otel-adapter-prd.md). It is broken down into logical phases, from initial setup to finalization.

## Phase 1: Project Setup & Core Dependencies

- [ ] **Project Scaffolding**
  - [ ] Create a new .NET Console Application project named `Dyalog.Hmon.OtelAdapter`.
  - [ ] Add a project reference to the existing `Dyalog.Hmon.Client.Lib`.
- [ ] **Add NuGet Packages**
  - [ ] `Microsoft.Extensions.Configuration` and related packages for config handling (`Configuration.Json`, `Configuration.Binder`, `Configuration.CommandLine`, `Configuration.EnvironmentVariables`).
  - [ ] `Serilog` and `Serilog.Sinks.Console` for adapter logging.
  - [ ] `OpenTelemetry` for the core SDK.
  - [ ] `OpenTelemetry.Exporter.Otlp` for exporting data.
  - [ ] `OpenTelemetry.Extensions.Hosting` to integrate with a generic host builder pattern.
- [ ] **Initial Structure**
  - [ ] Set up `Program.cs` to use the Generic Host (`Host.CreateDefaultBuilder`).
  - [ ] Create a main `AdapterService` class that will be run as a Hosted Service.

## Phase 2: Configuration & Logging

- [ ] **Implement Configuration Models**
  - [ ] Create C# `record` types that map to the `config.json` structure defined in the PRD (e.g., `AdapterConfig`, `HmonServerConfig`, `OtelExporterConfig`, etc.).
  - [ ] Use data annotations for validation (e.g., `[Required]`).
- [ ] **Implement Configuration Loading**
  - [ ] Configure the host builder to load settings from `config.json`, environment variables, and command-line arguments in the correct order of precedence.
  - [ ] Bind the loaded configuration to the C# model objects.
- [ ] **Implement Console Logging**
  - [ ] Configure Serilog as the logging provider for the application.
  - [ ] Ensure the `logLevel` from the configuration is used to set the minimum logging level.
  - [ ] Log the loaded configuration on startup (at Debug level, masking any secrets if they existed).

## Phase 3: HMON Orchestration & OTel Pipeline

- [ ] **Instantiate HMON Client**
  - [ ] In the `AdapterService`, create an instance of `Dyalog.Hmon.Client.Lib.HmonOrchestrator`.
- [ ] **Set up OpenTelemetry SDK**
  - [ ] Create a new class (e.g., `TelemetryFactory`) to manage OTel objects.
  - [ ] Configure the `ResourceBuilder` to add static service attributes (`service.name`).
  - [ ] Configure the `MeterProvider` for metrics.
  - [ ] Configure the `LoggerProvider` for logs.
  - [ ] Configure the `OtlpExporter` with the endpoint and protocol from the configuration.
- [ ] **Connect to HMON Interpreters**
  - [ ] In the `AdapterService.StartAsync`, use the loaded configuration to:
    - [ ] Call `orchestrator.AddServer()` for each server in the `hmonServers` list.
    - [ ] Call `orchestrator.StartListenerAsync()` if `pollListener` is configured.
- [ ] **Establish Main Processing Loop**
  - [ ] Create a long-running task that processes events from the orchestrator's event stream (`await foreach (var hmonEvent in orchestrator.Events)`).
  - [ ] Create a `ConcurrentDictionary` to store a `TelemetryFactory` instance for each `SessionId`, to manage resources and OTel instruments on a per-session basis.

## Phase 4: Core Data Mapping Logic (HMON -> OTel)

- [ ] **Implement Resource Attribute Mapping**
  - [ ] On the first `FactsReceivedEvent` for a new session, extract data from `HostFact` and `AccountInformationFact`.
  - [ ] Create a session-specific OTel `Resource` containing all the attributes listed in the PRD's "Enriched Resource Attributes" table.
  - [ ] Use this resource when creating the `Meter` and `Logger` for this session.
- [ ] **Implement Metric Mapping**
  - [ ] Inside the main event loop, add a `case` for `FactsReceivedEvent`.
  - [ ] For each `Fact` in the payload:
    - [ ] Look up the corresponding OTel metric details from the PRD's "Enriched Metrics" table.
    - [ ] Create or retrieve the OTel `Instrument` (e.g., `Gauge`, `Counter`).
    - [ ] Create the `TagList` of attributes (e.g., add `wsid` for workspace metrics).
    - [ ] Record the measurement using the instrument.
- [ ] **Implement Log Mapping**
  - [ ] **Signal/Notification Events:**
    - [ ] Handle `NotificationReceivedEvent`.
    - [ ] For `UntrappedSignal` and `TrappedSignal`:
      - [ ] Create a log record with `ERROR` or `WARN` severity.
      - [ ] Asynchronously call `orchestrator.GetFactsAsync` to fetch the detailed `ThreadsFact` for the reported `Tid`.
      - [ ] Populate the log record's attributes with the full `DMX`, `Stack`, and `ThreadInfo` data as a JSON string.
    - [ ] Handle other notifications (`WorkspaceResize`, etc.) by creating `INFO` level logs with their specific attributes.
  - [ ] **User Messages:**
    - [ ] Handle `UserMessageReceivedEvent` and translate it to an `INFO` log, capturing the UID and message body.
  - [ ] **Adapter-Generated Lifecycle Logs:**
    - [ ] Subscribe to the `orchestrator.ClientDisconnected` event and generate an `ERROR` log record with the appropriate attributes (`net.peer.name`, `error.message`, etc.).
    - [ ] Add `try-catch` blocks around connection attempts (`AddServer`) and generate `ERROR` logs on failure.

## Phase 5: Finalization and Reliability

- [ ] **Graceful Shutdown**
  - [ ] Implement `IAsyncDisposable` on `AdapterService`.
  - [ ] In the `DisposeAsync` method, properly dispose of the `HmonOrchestrator` and shut down the OTel `MeterProvider` and `LoggerProvider`.
  - [ ] Configure the application to listen for `Ctrl+C` (`Console.CancelKeyPress`) to trigger a graceful shutdown of the host.
- [ ] **Error Handling**
  - [ ] Ensure the main event processing loop is wrapped in a `try-catch` to prevent the adapter from crashing on a single malformed event. Log any such errors.
- [ ] **Documentation**
  - [ ] Create a `README.md` for the new `Dyalog.Hmon.OtelAdapter` project.
  - [ ] Document the configuration file schema and all command-line arguments.
  - [ ] Provide a simple "Getting Started" guide on how to run the adapter.
  - [ ] Add XML comments to the main classes (`AdapterService`, `TelemetryFactory`).
- [ ] **Testing (Future consideration)**
  - [ ] Design unit tests for the data mapping logic (e.g., a function that takes an `HmonEvent` and returns the expected OTel data structures).
  - [ ] Scope out an integration test that uses the `MockHmonServer`, a running adapter, and an in-memory OTel exporter to verify end-to-end data flow.
