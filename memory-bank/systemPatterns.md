# System Patterns: Dyalog.Hmon.Client

_Last reviewed: 2025-07-14 16:48 CEST_

## Architectural Overview
- **Orchestrator-Centric Design:** The HmonOrchestrator is the central component, managing all HMON connections and exposing a unified event stream.
- **Reactive Event Model:** All connection lifecycle events and data are delivered via a single IAsyncEnumerable<HmonEvent>, enabling asynchronous, event-driven consumption.
- **Stable Session Identity:** Each managed connection is assigned a stable SessionId (Guid), ensuring reliable tracking across reconnects.
- **Automatic Retry Logic:** Connections in SERVE mode are automatically retried using configurable policies for resilience.
- **Strong Typing:** All protocol messages and events are represented by strongly-typed C# records and enums.
- **Transparent Handshake Handling:** The library performs the HMON protocol handshake automatically and transparently when establishing connections in both POLL and SERVE modes. Consumers do not need to manage handshake logic; it is handled internally as part of connection setup.

## Key Patterns
- **Unified Event Stream:** Consumers process all events from all sessions in a single loop, simplifying application logic.
- **Separation of Concerns:** The orchestrator abstracts protocol and connection management, letting consumers focus on business logic.
- **Extensibility:** The design supports future enhancements and integration with various monitoring tools.

_Memory bank fully reviewed and confirmed up to date as of 2025-07-14 16:48 CEST._
