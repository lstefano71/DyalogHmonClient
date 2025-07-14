using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dyalog.Hmon.Client.Lib;

// Configuration
public record HmonOrchestratorOptions
{
    public RetryPolicy ConnectionRetryPolicy { get; init; } = new();
}

public record RetryPolicy
{
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromMinutes(1);
    public double BackoffMultiplier { get; init; } = 1.5;
}

// Lifecycle
public record ClientConnectedEventArgs(Guid SessionId, string Host, int Port, string? FriendlyName);
public record ClientDisconnectedEventArgs(Guid SessionId, string Host, int Port, string? FriendlyName, string Reason);

// Events
public abstract record HmonEvent(Guid SessionId);
public record FactsReceivedEvent(Guid SessionId, FactsResponse Facts) : HmonEvent(SessionId);
public record NotificationReceivedEvent(Guid SessionId, NotificationResponse Notification) : HmonEvent(SessionId);
public record LastKnownStateReceivedEvent(Guid SessionId, LastKnownStateResponse State) : HmonEvent(SessionId);
public record SubscribedResponseReceivedEvent(Guid SessionId, SubscribedResponse Response) : HmonEvent(SessionId);
public record RideConnectionReceivedEvent(Guid SessionId, RideConnectionResponse Response) : HmonEvent(SessionId);
public record UserMessageReceivedEvent(Guid SessionId, UserMessageResponse Message) : HmonEvent(SessionId);
public record UnknownCommandEvent(Guid SessionId, UnknownCommandResponse Error) : HmonEvent(SessionId);
public record MalformedCommandEvent(Guid SessionId, MalformedCommandResponse Error) : HmonEvent(SessionId);
public record InvalidSyntaxEvent(Guid SessionId, InvalidSyntaxResponse Error) : HmonEvent(SessionId);
public record DisallowedUidEvent(Guid SessionId, DisallowedUidResponse Error) : HmonEvent(SessionId);

// Payloads
public record FactsResponse(string? UID, int? Interval, IEnumerable<Fact> Facts);

[JsonDerivedType(typeof(HostFact), "Host")]
[JsonDerivedType(typeof(AccountInformationFact), "AccountInformation")]
[JsonDerivedType(typeof(WorkspaceFact), "Workspace")]
[JsonDerivedType(typeof(ThreadsFact), "Threads")]
[JsonDerivedType(typeof(SuspendedThreadsFact), "SuspendedThreads")]
[JsonDerivedType(typeof(ThreadCountFact), "ThreadCount")]
public abstract record Fact(int ID, string Name)
{
    public FactType FactType => (FactType)ID;
}

public record HostFact(MachineInfo Machine, InterpreterInfo Interpreter, CommsLayerInfo CommsLayer, RideInfo RIDE) : Fact(1, "Host");
public record MachineInfo(string Name, string User, int PID, object Desc, int AccessLevel);
public record InterpreterInfo(string Version, int BitWidth, bool IsUnicode, bool IsRuntime, string? SessionUUID);
public record CommsLayerInfo(string Version, string Address, int Port4, int Port6);
public record RideInfo(bool Listening, bool? HTTPServer, string? Version, string? Address, int? Port4, int? Port6);
public record AccountInformationFact(string UserIdentification, long ComputeTime, long ConnectTime, long KeyingTime) : Fact(2, "AccountInformation");
public record WorkspaceFact(string WSID, long Available, long Used, long Compactions, long GarbageCollections, long GarbagePockets, long FreePockets, long UsedPockets, long Sediment, long Allocation, long AllocationHWM, long TrapReserveWanted, long TrapReserveActual) : Fact(3, "Workspace");
public record ThreadsFact(IEnumerable<ThreadInfo> Values) : Fact(4, "Threads");
public record SuspendedThreadsFact(IEnumerable<ThreadInfo> Values) : Fact(5, "SuspendedThreads");
public record ThreadCountFact(int Total, int Suspended) : Fact(6, "ThreadCount");
public record ThreadInfo(int Tid, IEnumerable<StackInfo> Stack, bool Suspended, string State, string Flags, DmxInfo? DMX, ExceptionInfo? Exception);
public record StackInfo(bool Restricted, string? Description);
public record DmxInfo(bool Restricted, string? Category, int? DM, int? EM, int? EN, string? ENX, string? InternalLocation, string? Vendor, string? Message, int? OSError);
public record ExceptionInfo(bool Restricted, object? Source, string? StackTrace, string? Message);

public record NotificationResponse(string? UID, EventInfo Event, long? Size, int? Tid, IEnumerable<StackInfo>? Stack, DmxInfo? DMX, ExceptionInfo? Exception);
public record EventInfo(int ID, string Name);

public record LastKnownStateResponse(string? UID, string TS, ActivityInfo? Activity, LocationInfo? Location, [property: JsonPropertyName("WS FULL")] WsFullInfo? WsFull);
public record ActivityInfo(int Code, string TS);
public record LocationInfo(string Function, int Line, string TS);
public record WsFullInfo(string TS);

public record SubscribedResponse(string? UID, IEnumerable<SubscriptionStatus> Events);
public record SubscriptionStatus(int ID, string Name, int Value)
{
    public SubscriptionEvent EventEnum => (SubscriptionEvent)ID;
}

public record RideConnectionResponse(string? UID, bool Restricted, bool? Connect, int? Status);

public record UserMessageResponse(string? UID, JsonElement Message);

public record UnknownCommandResponse(string? UID, string Name);
public record MalformedCommandResponse(string? UID, string Name);
public record InvalidSyntaxResponse();
public record DisallowedUidResponse(string? UID, string Name);
