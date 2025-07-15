# HMon Hub Sample Implementation

- [x] Create new web app project for HMon Hub Sample.
- [x] Implement configuration model and loader (support CLI/env overrides).
- [x] Set up Serilog logging (console, extensible).
- [x] Implement auto-shutdown via config flag.
- [x] Build in-memory aggregation of facts.
- [x] Implement REST API endpoints (/facts, /status) using ASP.NET Core Minimal API.
- [x] Implement HMon server connectivity and poll listener.
- [x] Integrate orchestrator/session management and connect to HMon servers.
- [x] Aggregate facts in real time and update REST API endpoints with live data.
- [x] Implement WebSocket endpoint (/ws) for real-time event/fact updates.
- [x] Add event subscription and per-session event history (configurable).
- [x] Events are sent immediately through the websocket with timestamp and payload.
- [ ] Add robust error handling and reconnection logic.
- [ ] Write unit/integration tests and documentation.
- [ ] Update memory bank and TODO.md as progress is made.
