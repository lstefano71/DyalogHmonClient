# **Product Requirements Document: Dyalog.Hmon.Client Library**

| **Version** | **Date**      | **Author** | **Status** |
| :---------- | :------------ | :--------- | :--------- |
| 1.0         | July 14, 2025 | Gemini     | Final      |

## 1. Introduction & Vision

### 1.1. Vision

To provide the .NET ecosystem with a high-quality, modern, and robust library for managing communications with Dyalog APL interpreters via the Health Monitor (HMON) protocol. The library will serve as the foundational framework for building sophisticated monitoring and diagnostic tools in C#, such as real-time dashboards, logging services, and automated health-check utilities.

### 1.2. Purpose

This document outlines the requirements for **`Dyalog.Hmon.Client`**, a .NET library designed around a central **Orchestrator**. This orchestrator will manage a collection of HMON connections, abstracting away the complexities of the underlying protocol, connection management, and data flow. It will expose all activities through a unified, reactive event stream, enabling developers to build powerful monitoring tools with minimal boilerplate code.

## 2. Goals and Objectives

* **Primary Goal:** Deliver a high-level, session-management library that simplifies the development of applications monitoring multiple, concurrent Dyalog HMON sessions.
* **Key Objectives:**
  * **Unified Session Management:** Provide a single `HmonOrchestrator` class to configure and manage all HMON connections, whether they are initiated by the client (`SERVE` mode) or by the Dyalog interpreter (`POLL` mode).
  * **Resilient Connections:** Implement automatic, configurable retry logic for connections to interpreters in `SERVE` mode, ensuring sessions are maintained through transient network failures.
  * **Reactive Event Architecture:** Expose all connection lifecycle events and incoming data from all sessions through a single, asynchronous, strongly-typed event stream.
    * **Stable Session Identity:** Guarantee that each managed connection is associated with a stable, unique identifier, allowing consumer applications to reliably track state across disconnections and reconnections.
    * **Intuitive Interaction API:** Provide a clear and simple way for consumer applications to send commands to a specific Dyalog interpreter managed by the orchestrator.

## 3. Core Concepts

### 3.1. The HmonOrchestrator

The `HmonOrchestrator` is the central class and the primary entry point of the library. It acts as the "nervous system" for all HMON communications, managing a pool of connections and funneling all events into a predictable stream for the consumer.

### 3.2. The Stable `SessionId`

A `System.Guid` that uniquely identifies a managed session. This ID is the primary key for correlating all events and commands with a specific Dyalog interpreter.

* For an interpreter added via the active connection mechanism (to a `SERVE` instance), the `SessionId` is stable and persists across disconnections and reconnections.
* For an interpreter connecting to the orchestrator's listener (`POLL` instance), a new `SessionId` is generated for each new TCP connection.

### 3.3. The Unified Event Stream

The library's design is reactive. Instead of forcing the consumer to manage loops and callbacks for each connection, the orchestrator exposes a single `IAsyncEnumerable<HmonEvent>`. All data received from all managed interpreters (Facts, Notifications, Errors, etc.) is wrapped in an event object and pushed into this stream.

## 4. API Specification

Of course. Here is the completed section 4.1 of the Product Requirements Document, detailing the full list of interaction methods on the `HmonOrchestrator` class.

---

### 4.1. HmonOrchestrator

The primary public class for consumers.

```csharp
public class HmonOrchestrator : IAsyncDisposable
{
    // --- Configuration & Control ---

    /// <summary>
    /// Initializes a new instance of the HMON orchestrator.
    /// </summary>
    /// <param name="options">Optional configuration for retry policies and other behaviors.</param>
    public HmonOrchestrator(HmonOrchestratorOptions? options = null);

    /// <summary>
    /// Starts a listener to accept incoming connections from Dyalog interpreters in POLL mode.
    /// </summary>
    /// <param name="host">The local IP address or hostname to bind to. Use "0.0.0.0" to listen on all interfaces.</param>
    /// <param name="port">The TCP port to listen on.</param>
    /// <param name="ct">A cancellation token to stop the listener.</param>
    public Task StartListenerAsync(string host, int port, CancellationToken ct = default);

    /// <summary>
    /// Adds a Dyalog interpreter in SERVE mode to the managed pool. The orchestrator will
    /// attempt to connect immediately and will automatically retry on failure or disconnection
    /// according to the configured policy.
    /// </summary>
    /// <param name="host">The hostname or IP address of the Dyalog interpreter.</param>
    /// <param name="port">The TCP port the Dyalog interpreter is listening on.</param>
    /// <param name="friendlyName">An optional name for this session for easier identification.</param>
    /// <returns>The stable SessionId for this managed server.</returns>
    public Guid AddServer(string host, int port, string? friendlyName = null);

    /// <summary>
    // Stops managing a server previously added via AddServer. If the session is
    // currently connected, it will be disconnected. Auto-reconnect will cease.
    /// </summary>
    /// <param name="sessionId">The SessionId returned by the AddServer method.</param>
    public Task RemoveServerAsync(Guid sessionId);

    // --- Event Consumption ---

    /// <summary>
    /// Gets a unified asynchronous stream of all data events (Facts, Notifications, Errors, etc.)
    /// received from all managed sessions. This is the primary mechanism for consuming data.
    /// </summary>
    public IAsyncEnumerable<HmonEvent> Events { get; }

    /// <summary>
    /// An event that is raised when any Dyalog interpreter successfully connects to the orchestrator.
    /// </summary>
    public event Func<ClientConnectedEventArgs, Task>? ClientConnected;

    /// <summary>
    /// An event that is raised when any Dyalog interpreter is disconnected.
    /// </summary>
    public event Func<ClientDisconnectedEventArgs, Task>? ClientDisconnected;

    // --- Interaction ---

    /// <summary>
    /// Requests one or more "facts" (e.g., Host, Workspace info) from a specific session.
    /// This is a direct request-response operation.
    /// </summary>
    /// <param name="sessionId">The ID of the target session.</param>
    /// <param name="facts">An enumeration of the facts to retrieve.</param>
    /// <param name="ct">A cancellation token for the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the requested facts.</returns>
    public Task<FactsResponse> GetFactsAsync(Guid sessionId, IEnumerable<FactType> facts, CancellationToken ct = default);

    /// <summary>
    /// Requests the last known state of a specific session. This is useful for diagnostics
    /// when an interpreter may be unresponsive to other requests.
    /// </summary>
    /// <param name="sessionId">The ID of the target session.</param>
    /// <param name="ct">A cancellation token for the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the last known state.</returns>
    public Task<LastKnownStateResponse> GetLastKnownStateAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Instructs a session to begin polling for facts at a specified interval. The resulting
    /// Facts messages will be delivered through the main `Events` stream as `FactsReceivedEvent`.
    /// </summary>
    /// <param name="sessionId">The ID of the target session.</param>
    /// <param name="facts">An enumeration of the facts to poll for.</param>
    /// <param name="interval">The interval at which to send the facts.</param>
    /// <param name="ct">A cancellation token for the operation.</param>
    public Task PollFactsAsync(Guid sessionId, IEnumerable<FactType> facts, TimeSpan interval, CancellationToken ct = default);
    
    /// <summary>
    /// Instructs a session to stop polling for facts.
    /// </summary>
    /// <param name="sessionId">The ID of the target session.</param>
    /// <param name="ct">A cancellation token for the operation.</param>
    public Task StopFactsPollingAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Triggers an immediate "bump," causing a polling session to send a Facts message
    /// right away instead of waiting for the next interval.
    /// </summary>
    /// <param name="sessionId">The ID of the target session.</param>
    /// <param name="ct">A cancellation token for the operation.</param>
    public Task BumpFactsAsync(Guid sessionId, CancellationToken ct = default);
    
    /// <summary>
    /// Subscribes a session to one or more events (e.g., UntrappedSignal). The resulting
    /// Notification messages will be delivered through the main `Events` stream as `NotificationReceivedEvent`.
    /// </summary>
    /// <param name="sessionId">The ID of the target session.</param>
    /// <param name="events">An enumeration of the events to subscribe to.</param>
    /// <param name="ct">A cancellation token for the operation.</param>
    public Task SubscribeAsync(Guid sessionId, IEnumerable<SubscriptionEvent> events, CancellationToken ct = default);

    /// <summary>
    /// Requests that a session connects to a RIDE instance. Requires Access Level 3 on the interpreter.
    /// </summary>
    /// <param name="sessionId">The ID of the target session.</param>
    /// <param name="address">The address of the RIDE instance to connect to.</param>
    /// <param name="port">The port of the RIDE instance.</param>
    /// <param name="ct">A cancellation token for the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the status of the connection attempt.</returns>
    public Task<RideConnectionResponse> ConnectRideAsync(Guid sessionId, string address, int port, CancellationToken ct = default);

    /// <summary>
    /// Requests that a session disconnects from any active RIDE connection.
    /// </summary>
    /// <param name="sessionId">The ID of the target session.</param>
    /// <param name="ct">A cancellation token for the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the status of the disconnection attempt.</returns>
    public Task<RideConnectionResponse> DisconnectRideAsync(Guid sessionId, CancellationToken ct = default);
}
```

### 4.2. Configuration

```csharp
public record HmonOrchestratorOptions
{
    // Defines the policy for retrying connections to servers added via AddServer.
    public RetryPolicy ConnectionRetryPolicy { get; init; } = new();
}

public record RetryPolicy
{
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromMinutes(1);
    public double BackoffMultiplier { get; init; } = 1.5;
}
```

### 4.3. Data and Lifecycle Models

This section defines the C# `record` types used for orchestrator events. These models provide a strongly-typed representation of all data flowing from the Dyalog interpreters.

#### 4.3.1. Enumerations

These enums represent the well-known identifiers for HMON requests.

```csharp
/// <summary>
/// Represents the types of "facts" that can be requested from a Dyalog interpreter.
/// </summary>
public enum FactType
{
    Host,
    AccountInformation,
    Workspace,
    Threads,
    SuspendedThreads,
    ThreadCount
}

/// <summary>
/// Represents the types of events that a client can subscribe to.
/// </summary>
public enum SubscriptionEvent
{
    WorkspaceCompaction,
    WorkspaceResize,
    UntrappedSignal,
    TrappedSignal,
    ThreadSwitch,
    All
}
```

#### 4.3.2. Lifecycle Event Arguments

These records are used as arguments for the `HmonOrchestrator`'s C# lifecycle events.

```csharp
/// <summary>
/// Contains information about a newly established session.
/// </summary>
/// <param name="SessionId">The unique identifier for the session.</param>
/// <param name="Host">The remote host of the connected Dyalog interpreter.</param>
/// <param name="Port">The remote port of the connected Dyalog interpreter.</param>
/// <param name="FriendlyName">The user-defined friendly name for this session, if any.</param>
public record ClientConnectedEventArgs(Guid SessionId, string Host, int Port, string? FriendlyName);

/// <summary>
/// Contains information about a session that has been disconnected.
/// </summary>
/// <param name="SessionId">The unique identifier for the session.</param>
/// <param name="Host">The remote host of the Dyalog interpreter.</param>
/// <param name="Port">The remote port of the Dyalog interpreter.</param>
/// <param name="FriendlyName">The user-defined friendly name for this session, if any.</param>
/// <param name="Reason">A description of why the disconnection occurred.</param>
public record ClientDisconnectedEventArgs(Guid SessionId, string Host, int Port, string? FriendlyName, string Reason);
```

#### 4.3.3. Unified Data Event Stream Models

All data received from any managed session is wrapped in a derivative of the `HmonEvent` base record and delivered via the `HmonOrchestrator.Events` stream.

```csharp
/// <summary>
/// The abstract base record for all data events from the orchestrator.
/// </summary>
/// <param name="SessionId">The unique identifier of the session that produced this event.</param>
public abstract record HmonEvent(Guid SessionId);

// --- Primary Data Events ---
public record FactsReceivedEvent(Guid SessionId, FactsResponse Facts) : HmonEvent(SessionId);
public record NotificationReceivedEvent(Guid SessionId, NotificationResponse Notification) : HmonEvent(SessionId);
public record LastKnownStateReceivedEvent(Guid SessionId, LastKnownStateResponse State) : HmonEvent(SessionId);
public record SubscribedResponseReceivedEvent(Guid SessionId, SubscribedResponse Response) : HmonEvent(SessionId);
public record RideConnectionReceivedEvent(Guid SessionId, RideConnectionResponse Response) : HmonEvent(SessionId);
public record UserMessageReceivedEvent(Guid SessionId, UserMessageResponse Message) : HmonEvent(SessionId);

// --- Error and Protocol Status Events ---
public record UnknownCommandEvent(Guid SessionId, UnknownCommandResponse Error) : HmonEvent(SessionId);
public record MalformedCommandEvent(Guid SessionId, MalformedCommandResponse Error) : HmonEvent(SessionId);
public record InvalidSyntaxEvent(Guid SessionId, InvalidSyntaxResponse Error) : HmonEvent(SessionId);
public record DisallowedUidEvent(Guid SessionId, DisallowedUidResponse Error) : HmonEvent(SessionId);
```

#### 4.3.4. HMON Message Payload Models

These records define the strongly-typed structure of the JSON payloads for each HMON message type.

##### Facts Models

```csharp
/// <summary>
/// Represents the payload of a "Facts" response message.
/// </summary>
public record FactsResponse(string? UID, int? Interval, IEnumerable<Fact> Facts);

/// <summary>
/// An abstract base record for a polymorphic fact.
/// </summary>
[JsonDerivedType(typeof(HostFact), "Host")]
[JsonDerivedType(typeof(AccountInformationFact), "AccountInformation")]
[JsonDerivedType(typeof(WorkspaceFact), "Workspace")]
[JsonDerivedType(typeof(ThreadsFact), "Threads")]
[JsonDerivedType(typeof(SuspendedThreadsFact), "SuspendedThreads")]
[JsonDerivedType(typeof(ThreadCountFact), "ThreadCount")]
public abstract record Fact(int ID, string Name);

/// <summary>
/// Contains information about the host machine, interpreter, and comms layers.
/// </summary>
public record HostFact(MachineInfo Machine, InterpreterInfo Interpreter, CommsLayerInfo CommsLayer, RideInfo RIDE) : Fact(1, "Host");

/// <summary>
/// Contains facts about the host machine.
/// </summary>
public record MachineInfo(string Name, string User, int PID, object Desc, int AccessLevel);

/// <summary>
/// Contains facts about the Dyalog interpreter instance.
/// </summary>
public record InterpreterInfo(string Version, int BitWidth, bool IsUnicode, bool IsRuntime, string? SessionUUID);

/// <summary>
/// Contains facts about the Conga communications layer for HMON.
/// </summary>
public record CommsLayerInfo(string Version, string Address, int Port4, int Port6);

/// <summary>
/// Contains facts about the RIDE communications layer.
/// </summary>
public record RideInfo(bool Listening, bool? HTTPServer, string? Version, string? Address, int? Port4, int? Port6);

/// <summary>
/// Contains accounting information from ⎕AI.
/// </summary>
public record AccountInformationFact(string UserIdentification, long ComputeTime, long ConnectTime, long KeyingTime) : Fact(2, "AccountInformation");

/// <summary>
/// Contains statistics about the Dyalog workspace from 2000⌶.
/// </summary>
public record WorkspaceFact(string WSID, long Available, long Used, long Compactions, long GarbageCollections, long GarbagePockets, long FreePockets, long UsedPockets, long Sediment, long Allocation, long AllocationHWM, long TrapReserveWanted, long TrapReserveActual) : Fact(3, "Workspace");

/// <summary>
/// Contains information about all running threads.
/// </summary>
public record ThreadsFact(IEnumerable<ThreadInfo> Values) : Fact(4, "Threads");

/// <summary>
/// Contains information about all suspended threads.
/// </summary>
public record SuspendedThreadsFact(IEnumerable<ThreadInfo> Values) : Fact(5, "SuspendedThreads");

/// <summary>
/// Contains a summary of thread counts.
/// </summary>
public record ThreadCountFact(int Total, int Suspended) : Fact(6, "ThreadCount");

// Nested models for thread information
public record ThreadInfo(int Tid, IEnumerable<StackInfo> Stack, bool Suspended, string State, string Flags, DmxInfo? DMX, ExceptionInfo? Exception);
public record StackInfo(bool Restricted, string? Description);
public record DmxInfo(bool Restricted, string? Category, int? DM, int? EM, int? EN, string? ENX, string? InternalLocation, string? Vendor, string? Message, int? OSError);
public record ExceptionInfo(bool Restricted, object? Source, string? StackTrace, string? Message);
```

##### Other Payload Models

```csharp
/// <summary>
/// Represents the payload of a "Notification" response message.
/// </summary>
public record NotificationResponse(string? UID, EventInfo Event, long? Size, int? Tid, IEnumerable<StackInfo>? Stack, DmxInfo? DMX, ExceptionInfo? Exception);
public record EventInfo(int ID, string Name);

/// <summary>
/// Represents the payload of a "LastKnownState" response message.
/// </summary>
public record LastKnownStateResponse(string? UID, string TS, ActivityInfo? Activity, LocationInfo? Location, [property: JsonPropertyName("WS FULL")] WsFullInfo? WsFull);
public record ActivityInfo(int Code, string TS);
public record LocationInfo(string Function, int Line, string TS);
public record WsFullInfo(string TS);

/// <summary>
/// Represents the payload of a "Subscribed" response message, confirming subscription settings.
/// </summary>
public record SubscribedResponse(string? UID, IEnumerable<SubscriptionStatus> Events);
public record SubscriptionStatus(int ID, string Name, int Value);

/// <summary>
/// Represents the payload of a "RideConnection" response message.
/// </summary>
public record RideConnectionResponse(string? UID, bool Restricted, bool? Connect, int? Status);

/// <summary>
/// Represents a user-defined message sent from the interpreter via 111⌶.
/// </summary>
public record UserMessageResponse(string? UID, JsonElement Message);

/// <summary>
/// Represents the "UnknownCommand" error response.
/// </summary>
public record UnknownCommandResponse(string? UID, string Name);

/// <summary>
/// Represents the "MalformedCommand" error response.
/// </summary>
public record MalformedCommandResponse(string? UID, string Name);

/// <summary>
/// Represents the "InvalidSyntax" error response.
/// </summary>
public record InvalidSyntaxResponse();

/// <summary>
/// Represents the "DisallowedUID" error response.
/// </summary>
public record DisallowedUidResponse(string? UID, string Name);
```

## 5. Usage Example / Consumer Workflow

The following example demonstrates how a consumer would use the `HmonOrchestrator` to build a simple console-based monitoring application.

```csharp
public async Task RunMonitoringService(CancellationToken cancellationToken)
{
    // 1. Instantiate the orchestrator.
    await using var orchestrator = new HmonOrchestrator();

    // 2. Subscribe to lifecycle events to log connections/disconnections
    //    and set up initial monitoring on new connections.
    orchestrator.ClientConnected += async (args) =>
    {
        Console.WriteLine($"[+] CONNECTED: {args.FriendlyName ?? args.Host} (Session: {args.SessionId})");
        
        // When a client connects, automatically subscribe to untrapped signals
        // and start polling for basic workspace and thread facts.
        await orchestrator.SubscribeAsync(args.SessionId, new[] { SubscriptionEvent.UntrappedSignal }, cancellationToken);
        await orchestrator.PollFactsAsync(args.SessionId, new[] { FactType.Workspace, FactType.ThreadCount }, TimeSpan.FromSeconds(5), cancellationToken);
    };

    orchestrator.ClientDisconnected += (args) =>
    {
        Console.WriteLine($"[-] DISCONNECTED: {args.FriendlyName ?? args.Host}. Reason: {args.Reason}");
        return Task.CompletedTask;
    };

    // 3. Configure the desired state: add servers to monitor and start the listener.
    orchestrator.AddServer("192.168.1.100", 4502, "Primary App Server");
    orchestrator.AddServer("192.168.1.101", 4502, "Reporting Server");
    await orchestrator.StartListenerAsync("0.0.0.0", 7000, cancellationToken); // For POLL-mode interpreters

    // 4. Process the unified event stream from all connections in a single loop.
    try
    {
        await foreach (var hmonEvent in orchestrator.Events.WithCancellation(cancellationToken))
        {
            switch (hmonEvent)
            {
                case FactsReceivedEvent e:
                    // Find the friendly name for the SessionId and update internal state.
                    Console.WriteLine($"Facts from {e.SessionId}: WS Used = {e.Facts.Workspace.Used} bytes");
                    break;
                
                case NotificationReceivedEvent e when e.Notification.Event.Name == "UntrappedSignal":
                    // Send a high-priority alert.
                    Console.Error.WriteLine($"!!! ALERT: Untrapped signal from session {e.SessionId}!");
                    break;
                
                // Handle other event types...
            }
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Monitoring service shutting down.");
    }
}
```

## 6. Non-Functional Requirements

* **Performance:** The library must be highly efficient, using modern .NET techniques like `System.IO.Pipelines` and `System.Text.Json` source generation to minimize CPU and memory overhead, especially when handling many concurrent connections and high-frequency data streams.
* **Reliability:** The orchestrator must be robust, correctly managing the state of all connections and ensuring prompt detection and signaling of disconnections. The background retry logic must be resilient and not leak resources.
* **Usability:** The API should be intuitive and self-documenting. Comprehensive XML documentation must be provided for excellent IntelliSense support.
* **Testability:** The orchestrator's design should allow for comprehensive unit and integration testing. Key components should be interface-based where appropriate to facilitate mocking.

## 7. Technical Stack

* **Language:** C# 13
* **Framework:** .NET 9.0
* **Core Dependencies:**
  * `System.Net.Sockets`
  * `System.IO.Pipelines`
  * `System.Text.Json` (with Source Generation)
  * `System.Threading.Channels` (for internal event queueing)

## 8. Out of Scope

* **Client Implementations:** This project covers the `Dyalog.Hmon.Client` library only. Specific client applications (e.g., GUI dashboards, command-line tools, web services) are consumers of this library and are considered separate projects.
* **Protocol Extensions:** The library will faithfully implement the existing HMON protocol as specified. It will not propose or implement any extensions to the protocol.
