# TODO: Dyalog.Hmon.Client Implementation

This document outlines the tasks required to implement the `Dyalog.Hmon.Client` library as specified in the [PRD](docs/hmonclient-prd.md).

## Phase 1: Core Infrastructure

- [x] **Project Setup**
    - [x] Create solution file `Dyalog.Hmon.Client.sln`.
    - [x] Create `Dyalog.Hmon.Client.Lib` class library project.
    - [x] Create `Dyalog.Hmon.Client.Console` console application project.
    - [x] Create `Dyalog.Hmon.Client.Tests` test project.
    - [x] Add project references.
- [x] **Core Concepts Implementation**
    - [x] Implement the `HmonOrchestrator` class shell.
    - [x] Define the `SessionId` concept using `System.Guid`.
    - [x] Set up the unified event stream using `System.Threading.Channels` and expose it as `IAsyncEnumerable<HmonEvent>`.

## Phase 2: Configuration and Data Models

- [x] **Configuration Models**
    - [x] Implement `HmonOrchestratorOptions` record.
    - [x] Implement `RetryPolicy` record.
- [x] **Enumerations**
    - [x] Implement `FactType` enum.
    - [x] Implement `SubscriptionEvent` enum.
- [x] **Lifecycle Event Arguments**
    - [x] Implement `ClientConnectedEventArgs` record.
    - [x] Implement `ClientDisconnectedEventArgs` record.
- [x] **Unified Data Event Stream Models**
    - [x] Implement the base `HmonEvent` record.
    - [x] Implement all `HmonEvent` derivatives (`FactsReceivedEvent`, `NotificationReceivedEvent`, etc.).
- [x] **HMON Message Payload Models**
    - [x] Implement `FactsResponse` and all `Fact`-derived records (`HostFact`, `WorkspaceFact`, etc.).
    - [x] Implement `NotificationResponse` and related models.
    - [x] Implement `LastKnownStateResponse` and related models.
    - [x] Implement `SubscribedResponse` and related models.
    - [x] Implement `RideConnectionResponse`.
    - [x] Implement `UserMessageResponse`.
    - [x] Implement all error response models (`UnknownCommandResponse`, etc.).
    - [x] Configure JSON serialization/deserialization with `System.Text.Json`, including source generation for performance and handling polymorphic types (`Fact`).

## Phase 3: HmonOrchestrator API Implementation

- [x] **Constructor and Configuration**
    - [x] Implement the `HmonOrchestrator` constructor accepting `HmonOrchestratorOptions`.
- [ ] **Connection Management**
    - [x] Implement `StartListenerAsync` for POLL mode.
    - [x] Implement `AddServer` for SERVE mode, including the connection and retry logic.
    - [x] Implement `RemoveServerAsync`.
- [x] **Event Handling**
    - [x] Implement the `ClientConnected` event.
    - [x] Implement the `ClientDisconnected` event.
- [x] **Interaction API**
    - [x] Implement `GetFactsAsync`.
    - [x] Implement `GetLastKnownStateAsync`.
    - [x] Implement `PollFactsAsync`.
    - [x] Implement `StopFactsPollingAsync`.
    - [x] Implement `BumpFactsAsync`.
    - [x] Implement `SubscribeAsync`.
    - [x] Implement `ConnectRideAsync`.
    - [x] Implement `DisconnectRideAsync`.
- [x] **Disposal**
    - [x] Implement `IAsyncDisposable` on `HmonOrchestrator` to clean up connections and resources.

## Phase 4: Non-Functional Requirements & Finalization

- [x] **Performance**
    - [x] Integrate `System.IO.Pipelines` for efficient network I/O.
    - [x] Ensure `System.Text.Json` source generation is correctly implemented.
- [ ] **Reliability**
    - [ ] Thoroughly test connection retry logic.
    - [ ] Ensure robust error handling and state management.
- [ ] **Usability**
    - [ ] Add comprehensive XML documentation to all public APIs.
- [ ] **Testability**
    - [ ] Write unit tests for all core logic.
    - [ ] Write integration tests for the `HmonOrchestrator` API.
- [ ] **Example Application**
    - [ ] Implement the consumer workflow example in the `Dyalog.Hmon.Client.Console` project.
- [ ] **Documentation**
    - [ ] Review and update all documentation.
    - [ ] Ensure the `README.md` is up-to-date.
