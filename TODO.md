# Project TODO List

This file tracks the major phases and tasks for the Dyalog.Hmon.Client project. Each item is numbered for reference and will be checked off with a timestamp when completed.

## TODO

1. Initialize git repository [x] (2025-07-14)
2. Create .gitignore with standard C#/.NET rules [x] (2025-07-14)
3. Reference main PRD (docs/hmonclient-prd.md) in all instruction files [x] (2025-07-14)
4. Project scaffolding:
    4.1. Create library project for Dyalog.Hmon.Client
    4.2. Create console project for experimentation
    4.3. Create test project for unit/integration tests
    4.4. Create solution file referencing all three projects
5. Implement core library (per PRD):
    5.1. Define HmonOrchestrator class and public API
    5.2. Implement configuration and retry policy models
    5.3. Implement FactType and SubscriptionEvent enums
    5.4. Implement lifecycle event argument records
    5.5. Implement HmonEvent base and derived event records
    5.6. Implement all HMON message payload models (Facts, Notifications, etc.)
    5.7. Implement connection management (SERVE and POLL modes)
    5.8. Implement unified event stream (IAsyncEnumerable<HmonEvent>)
    5.9. Implement command and subscription methods (GetFactsAsync, PollFactsAsync, etc.)
    5.10. Implement error and protocol status event handling
    5.11. Ensure stable SessionId management
    5.12. Integrate System.IO.Pipelines and System.Text.Json source generation
    5.13. Integrate logging (Serilog)
    5.14. Integrate embedded storage if needed (SQLite)
6. Implement console app usage example:
    6.1. Instantiate orchestrator and configure servers/listener
    6.2. Subscribe to lifecycle events and log output
    6.3. Demonstrate event stream processing and command usage
7. Implement tests (per PRD non-functional requirements):
    7.1. Unit tests for orchestrator API and models
    7.2. Integration tests for connection management and event flow
    7.3. Mocking and interface-based tests for reliability
    7.4. Performance and resource-leak tests
8. Documentation and instructions:
    8.1. Update XML docs for public API
    8.2. Update usage examples in docs/
    8.3. Update/maintain this TODO list with timestamps
    8.4. Refine instruction files as project evolves

---
Completed items are marked [x] with a timestamp. In-progress and future items are unchecked.
