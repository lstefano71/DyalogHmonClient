# Dyalog.Hmon.Client API User Guide

This guide describes how to use the Dyalog.Hmon.Client library for monitoring and interacting with Dyalog APL servers.

## 1. Getting Started

Add a reference to `Dyalog.Hmon.Client.Lib` in your .NET project.

```csharp
using Dyalog.Hmon.Client.Lib;
```

## 2. Creating an Orchestrator

Create an instance of `HmonOrchestrator`:

```csharp
var orchestrator = new HmonOrchestrator();
```

## 3. Connecting to Servers

You can connect to HMON servers in two ways:

### a) Listening for Incoming Connections

Start a listener to accept incoming HMON server connections:

```csharp
await orchestrator.StartListenerAsync("0.0.0.0", 8080, cancellationToken);
```

### b) Adding a Remote Server (Outgoing Connection)

Connect to a remote HMON server by host and port:

```csharp
var sessionId = orchestrator.AddServer("192.168.1.100", 8080, "My Remote Server");
```

This returns a session ID you can use for subscriptions and polling.

## 4. Subscribing to Events and Polling Facts

### Classic API

Subscribe to events and start polling facts:

```csharp
await orchestrator.SubscribeAsync(sessionId, new[] { SubscriptionEvent.UntrappedSignal }, cancellationToken);
await orchestrator.PollFactsAsync(sessionId, new[] { FactType.Workspace, FactType.ThreadCount }, TimeSpan.FromSeconds(5), cancellationToken);
```

### Fluent API (Builder Pattern)

Use the `SessionMonitorBuilder` for fluent setup:

```csharp
var builder = new SessionMonitorBuilder(orchestrator, sessionId)
    .SubscribeTo(SubscriptionEvent.UntrappedSignal)
    .PollFacts(TimeSpan.FromSeconds(5), FactType.Workspace, FactType.ThreadCount)
    .OnFactChanged(async fact => { /* handle fact */ })
    .OnEvent(async evt => { /* handle event */ })
    .WithCancellation(cancellationToken);

await builder.StartAsync();
```

## 5. Accessing Facts

### Get Latest Fact

```csharp
var workspace = orchestrator.GetFact<WorkspaceFact>(sessionId);
```

### Get Fact with Timestamp

```csharp
var (fact, lastUpdated) = orchestrator.GetFactWithTimestamp<WorkspaceFact>(sessionId);
if (fact != null)
{
    Console.WriteLine($"Workspace last updated at {lastUpdated:u}");
}
```

## 6. Handling Session Updates

Subscribe to unified session updates:

```csharp
orchestrator.OnSessionUpdated += (sessionId, fact) =>
{
    Console.WriteLine($"Session {sessionId} updated fact {fact.GetType().Name}");
};
```

## 7. Error and Diagnostics Handling

Subscribe to error events:

```csharp
orchestrator.OnError += (ex, sessionId) =>
{
    Console.WriteLine($"Error in session {sessionId}: {ex.Message}");
};
```

## 8. Disposing

Dispose the orchestrator when done:

```csharp
await orchestrator.DisposeAsync();
```

---

For more details, see the [sample client](sample-client.md) and the inline XML documentation in the source code (`Dyalog.Hmon.Client.Lib`).

---

# API Reference

## Table of Contents

- [HmonOrchestrator](#hmonorchestrator)
- [SessionMonitorBuilder](#sessionmonitorbuilder)
- [Data Models & Events](#data-models--events)
- [Enums](#enums)
- [Configuration & Utilities](#configuration--utilities)

---

## HmonOrchestrator

```csharp
public class HmonOrchestrator : IAsyncDisposable
```
Central orchestrator for managing HMON connections and exposing a unified event stream.

### Events & Properties

- `IAsyncEnumerable<HmonEvent> Events`  
  Unified asynchronous event stream for all HMON events.

- `event Action<Guid, Fact>? OnSessionUpdated`  
  Fired when any fact for a session is updated.

- `event Func<ClientConnectedEventArgs, Task>? ClientConnected`  
  Fired when a client connects.

- `event Func<ClientDisconnectedEventArgs, Task>? ClientDisconnected`  
  Fired when a client disconnects.

- `event Action<Exception, Guid?>? OnError`  
  Fired on error or diagnostic event.

### Methods

```csharp
public Task StartListenerAsync(string host, int port, CancellationToken ct = default)
```
Starts a TCP listener for incoming HMON connections (POLL mode).

```csharp
public Guid AddServer(string host, int port, string? friendlyName = null)
```
Adds a remote HMON server (SERVE mode) and starts connection management.

```csharp
public Task RemoveServerAsync(Guid sessionId)
```
Removes and disposes a server connection by session ID.

```csharp
public Task<FactsResponse> GetFactsAsync(Guid sessionId, IEnumerable<FactType> facts, CancellationToken ct = default)
```
Requests a one-time snapshot of facts from the interpreter.

```csharp
public Task<LastKnownStateResponse> GetLastKnownStateAsync(Guid sessionId, CancellationToken ct = default)
```
Requests a high-priority status report from the interpreter.

```csharp
public Task PollFactsAsync(Guid sessionId, IEnumerable<FactType> facts, TimeSpan interval, CancellationToken ct = default)
```
Starts polling facts from the interpreter at a given interval.

```csharp
public Task StopFactsPollingAsync(Guid sessionId, CancellationToken ct = default)
```
Stops any active facts polling for the given session.

```csharp
public Task BumpFactsAsync(Guid sessionId, CancellationToken ct = default)
```
Triggers an immediate facts message from an active poll.

```csharp
public Task SubscribeAsync(Guid sessionId, IEnumerable<SubscriptionEvent> events, CancellationToken ct = default)
```
Subscribes to interpreter events for the given session.

```csharp
public Task<RideConnectionResponse> ConnectRideAsync(Guid sessionId, string address, int port, CancellationToken ct = default)
```
Requests the interpreter to connect to a RIDE client.

```csharp
public Task<RideConnectionResponse> DisconnectRideAsync(Guid sessionId, CancellationToken ct = default)
```
Requests the interpreter to disconnect from any RIDE client.

```csharp
public T? GetFact<T>(Guid sessionId) where T : Fact
public Fact? GetFact(Guid sessionId, Type factType)
public (T? Fact, DateTimeOffset? LastUpdated) GetFactWithTimestamp<T>(Guid sessionId) where T : Fact
public (Fact? Fact, DateTimeOffset? LastUpdated) GetFactWithTimestamp(Guid sessionId, Type factType)
```
Fact cache accessors for latest facts and timestamps.

```csharp
public ValueTask DisposeAsync()
```
Disposes all managed connections and resources.

---

## SessionMonitorBuilder

```csharp
public class SessionMonitorBuilder
```
Fluent builder for configuring session monitoring.

### Constructor

```csharp
public SessionMonitorBuilder(HmonOrchestrator orchestrator, Guid sessionId)
```

### Fluent Methods

```csharp
public SessionMonitorBuilder SubscribeTo(params SubscriptionEvent[] events)
public SessionMonitorBuilder PollFacts(TimeSpan interval, params FactType[] facts)
public SessionMonitorBuilder OnFactChanged(Func<Fact, Task> handler)
public SessionMonitorBuilder OnEvent(Func<HmonEvent, Task> handler)
public SessionMonitorBuilder WithCancellation(CancellationToken ct)
public Task StartAsync()
```
Chain these methods to configure subscriptions, polling, event handlers, and cancellation.

---

## Data Models & Events

### Payloads & Responses

```csharp
public interface IUidPayload { string? UID { get; set; } }
public record GetFactsPayload(int[] Facts) : IUidPayload
public record PollFactsPayload(int[] Facts, int Interval) : IUidPayload
public record SubscribePayload(int[] Events) : IUidPayload
public record LastKnownStatePayload() : IUidPayload
public record FactsResponse(string? UID, int? Interval, IEnumerable<Fact> Facts)
```

### Lifecycle Events

```csharp
public record ClientConnectedEventArgs(Guid SessionId, string Host, int Port, string? FriendlyName)
public record ClientDisconnectedEventArgs(Guid SessionId, string Host, int Port, string? FriendlyName, string Reason)
```

### Event Stream

```csharp
public abstract record HmonEvent(Guid SessionId)
public record FactsReceivedEvent(Guid SessionId, FactsResponse Facts) : HmonEvent(SessionId)
public record NotificationReceivedEvent(Guid SessionId, NotificationResponse Notification) : HmonEvent(SessionId)
public record LastKnownStateReceivedEvent(Guid SessionId, LastKnownStateResponse State) : HmonEvent(SessionId)
public record SubscribedResponseReceivedEvent(Guid SessionId, SubscribedResponse Response) : HmonEvent(SessionId)
public record RideConnectionReceivedEvent(Guid SessionId, RideConnectionResponse Response) : HmonEvent(SessionId)
public record UserMessageReceivedEvent(Guid SessionId, UserMessageResponse Message) : HmonEvent(SessionId)
public record UnknownCommandEvent(Guid SessionId, UnknownCommandResponse Error) : HmonEvent(SessionId)
public record MalformedCommandEvent(Guid SessionId, MalformedCommandResponse Error) : HmonEvent(SessionId)
public record InvalidSyntaxEvent(Guid SessionId, InvalidSyntaxResponse Error) : HmonEvent(SessionId)
public record DisallowedUidEvent(Guid SessionId, DisallowedUidResponse Error) : HmonEvent(SessionId)
```

### Facts & Related Models

```csharp
public abstract record Fact(int ID, string Name)
public record HostFact(MachineInfo Machine, InterpreterInfo Interpreter, CommsLayerInfo? CommsLayer, RideInfo RIDE) : Fact
public record AccountInformationFact(int UserIdentification, long ComputeTime, long ConnectTime, long KeyingTime) : Fact
public record WorkspaceFact(string WSID, long Available, long Used, long Compactions, long GarbageCollections, long GarbagePockets, long FreePockets, long UsedPockets, long Sediment, long Allocation, long AllocationHWM, long TrapReserveWanted, long TrapReserveActual) : Fact
public record ThreadsFact(IEnumerable<ThreadInfo> Values) : Fact
public record SuspendedThreadsFact(IEnumerable<ThreadInfo> Values) : Fact
public record ThreadCountFact(int Total, int Suspended) : Fact
public record ThreadInfo(int Tid, IEnumerable<StackInfo> Stack, bool Suspended, string State, string Flags, DmxInfo? DMX, ExceptionInfo? Exception)
public record StackInfo(bool Restricted, string? Description)
public record DmxInfo(bool Restricted, string? Category, string[]? DM, string? EM, int? EN, int? ENX, InternalLocationInfo? InternalLocation, string? Vendor, string? Message, object? OSError)
public record ExceptionInfo(bool Restricted, object? Source, string? StackTrace, string? Message)
```

### Other Payloads

```csharp
public record NotificationResponse(string? UID, EventInfo Event, long? Size, int? Tid, IEnumerable<StackInfo>? Stack, DmxInfo? DMX, ExceptionInfo? Exception)
public record EventInfo(int ID, string Name)
public record LastKnownStateResponse(string? UID, string TS, ActivityInfo? Activity, LocationInfo? Location, WsFullInfo? WsFull)
public record ActivityInfo(int Code, string TS)
public record LocationInfo(string Function, int Line, string TS)
public record WsFullInfo(string TS)
public record SubscribedResponse(string? UID, IEnumerable<SubscriptionStatus> Events)
public record SubscriptionStatus(int ID, string Name, int Value)
public record RideConnectionResponse(string? UID, bool Restricted, bool? Connect, int? Status)
public record UserMessageResponse(string? UID, JsonElement Message)
public record UnknownCommandResponse(string? UID, string Name)
public record MalformedCommandResponse(string? UID, string Name)
public record InvalidSyntaxResponse()
public record DisallowedUidResponse(string? UID, string Name)
public record InternalLocationInfo(string File, int Line)
```

---

## Enums

```csharp
public enum FactType
{
  Host = 1,
  AccountInformation = 2,
  Workspace = 3,
  Threads = 4,
  SuspendedThreads = 5,
  ThreadCount = 6
}

public enum SubscriptionEvent
{
  WorkspaceCompaction = 1,
  WorkspaceResize = 2,
  UntrappedSignal = 3,
  TrappedSignal = 4,
  ThreadSwitch = 5,
  All = 6
}
```

---

## Configuration & Utilities

```csharp
public record HmonOrchestratorOptions
{
  public RetryPolicy ConnectionRetryPolicy { get; init; } = new();
}

public record RetryPolicy
{
  public TimeSpan InitialDelay { get; init; }
  public TimeSpan MaxDelay { get; init; }
  public double BackoffMultiplier { get; init; }
}

public class HMonBooleanConverter : JsonConverter<bool>
public class FactJsonConverter : JsonConverter<Fact>
public class InternalLocationInfoConverter : JsonConverter<InternalLocationInfo?>
```
