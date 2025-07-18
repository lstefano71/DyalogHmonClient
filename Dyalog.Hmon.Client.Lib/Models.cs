using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Dyalog.Hmon.Client.Lib;
// Marker interface for payloads that support UID correlation
public interface IUidPayload
{
  string? UID { get; set; }
}
// Payloads for commands that require UID correlation
public record GetFactsPayload(int[] Facts) : IUidPayload { public string? UID { get; set; } }
public record PollFactsPayload(int[] Facts, int Interval) : IUidPayload { public string? UID { get; set; } }
public record SubscribePayload(int[] Events) : IUidPayload { public string? UID { get; set; } }
public record LastKnownStatePayload() : IUidPayload { public string? UID { get; set; } }
// Configuration
public record HmonOrchestratorOptions
{
    public RetryPolicy ConnectionRetryPolicy { get; init; } = new();

    /// <summary>
    /// The maximum age of a cached fact before it is considered stale and invalid.
    /// Defaults to 5 minutes.
    /// </summary>
    public TimeSpan FactCacheTTL { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The default timeout for all commands sent to the interpreter.
    /// This prevents calls from hanging indefinitely. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan DefaultCommandTimeout { get; init; } = TimeSpan.FromSeconds(30);
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
// NEW: Lifecycle events are now part of the unified stream
public record SessionConnectedEvent(Guid SessionId, string Host, int Port, string? FriendlyName) : HmonEvent(SessionId);
public record SessionDisconnectedEvent(Guid SessionId, string Host, int Port, string? FriendlyName, string Reason) : HmonEvent(SessionId);
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
public abstract record Fact(int ID, string Name)
{
  public FactType FactType => (FactType)ID;
}
public class FactJsonConverter : JsonConverter<Fact>
{
  public override Fact? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    using var doc = JsonDocument.ParseValue(ref reader);
    var root = doc.RootElement;
    if (!root.TryGetProperty("Name", out var nameProp))
      throw new JsonException("Missing 'Name' property for Fact polymorphic deserialization.");
    var name = nameProp.GetString();
    var id = root.GetProperty("ID").GetInt32();
    // For facts with a 'Value' property, use it for deserialization
    if (root.TryGetProperty("Value", out var valueProp)) {
      switch (name) {
        case "Workspace":
          var ws = valueProp.Deserialize<WorkspaceFact>(options);
          // WorkspaceFact's record constructor sets ID/Name, but we want to preserve the protocol's ID/Name
          return ws with { ID = id, Name = name };
        case "ThreadCount":
          var tc = valueProp.Deserialize<ThreadCountFact>(options);
          return tc with { ID = id, Name = name };
        case "Threads":
          var threads = valueProp.Deserialize<ThreadsFact>(options);
          return threads with { ID = id, Name = name };
        case "SuspendedThreads":
          var sthreads = valueProp.Deserialize<SuspendedThreadsFact>(options);
          return sthreads with { ID = id, Name = name };
        case "AccountInformation":
          var acc = valueProp.Deserialize<AccountInformationFact>(options);
          return acc with { ID = id, Name = name };
        case "Host":
          var host = valueProp.Deserialize<HostFact>(options);
          return host with { ID = id, Name = name };
        default:
          throw new JsonException($"Unknown Fact type: {name}");
      }
    }
    // Fallback: try to deserialize the whole object (legacy or non-Value facts)
    return name switch {
      "Host" => root.Deserialize<HostFact>(options),
      "AccountInformation" => root.Deserialize<AccountInformationFact>(options),
      "Workspace" => root.Deserialize<WorkspaceFact>(options),
      "Threads" => root.Deserialize<ThreadsFact>(options),
      "SuspendedThreads" => root.Deserialize<SuspendedThreadsFact>(options),
      "ThreadCount" => root.Deserialize<ThreadCountFact>(options),
      _ => throw new JsonException($"Unknown Fact type: {name}")
    };
  }
  public override void Write(Utf8JsonWriter writer, Fact value, JsonSerializerOptions options)
  {
    JsonSerializer.Serialize(writer, (object)value, value.GetType(), options);
  }
}
public record HostFact(MachineInfo Machine, InterpreterInfo Interpreter, CommsLayerInfo? CommsLayer, RideInfo RIDE) : Fact(1, "Host");
public record MachineInfo(string Name, string User, int PID, object Desc, int AccessLevel);
public record InterpreterInfo(
    string Version,
    int BitWidth,
    [property: JsonConverter(typeof(HMonBooleanConverter))] bool IsUnicode,
    [property: JsonConverter(typeof(HMonBooleanConverter))] bool IsRuntime,
    string? SessionUUID
);
public record CommsLayerInfo(string? Version, string? Address, int Port4, int Port6);
public record RideInfo(
    [property: JsonConverter(typeof(HMonBooleanConverter))] bool Listening,
    [property: JsonConverter(typeof(HMonBooleanConverter))] bool? HTTPServer,
    string? Version,
    string? Address,
    int? Port4,
    int? Port6
);
public record AccountInformationFact(int UserIdentification, long ComputeTime, long ConnectTime, long KeyingTime) : Fact(2, "AccountInformation");
public record WorkspaceFact(string WSID, long Available, long Used, long Compactions,
  long GarbageCollections, long GarbagePockets, long FreePockets, long UsedPockets,
  long Sediment, long Allocation, long AllocationHWM, long TrapReserveWanted,
  long TrapReserveActual) : Fact(3, "Workspace");
public record ThreadsFact(IEnumerable<ThreadInfo> Values) : Fact(4, "Threads");
public record SuspendedThreadsFact(IEnumerable<ThreadInfo> Values) : Fact(5, "SuspendedThreads");
public record ThreadCountFact(int Total, int Suspended) : Fact(6, "ThreadCount");
public record ThreadInfo(
    int Tid,
    IEnumerable<StackInfo> Stack,
    [property: JsonConverter(typeof(HMonBooleanConverter))] bool Suspended,
    string State,
    string Flags,
    DmxInfo? DMX,
    ExceptionInfo? Exception
);
public record StackInfo(
    [property: JsonConverter(typeof(HMonBooleanConverter))] bool Restricted,
    string? Description
);
/// <summary>
/// Represents ⎕DMX information for a thread, as per RFC 002 and protocol sample.
/// </summary>
public record DmxInfo(
    [property: JsonConverter(typeof(HMonBooleanConverter))] bool Restricted,
    string? Category,
    string[]? DM,
    string? EM,
    int? EN,
    int? ENX,
    [property: JsonConverter(typeof(InternalLocationInfoConverter))] InternalLocationInfo? InternalLocation,
    string? Vendor,
    string? Message,
    [property: JsonConverter(typeof(OSErrorInfoJsonConverter))] OSErrorInfo? OSError
);
public record ExceptionInfo(
    [property: JsonConverter(typeof(HMonBooleanConverter))] bool Restricted,
    object? Source,
    string? StackTrace,
    string? Message,
    [property: JsonConverter(typeof(OSErrorInfoJsonConverter))] OSErrorInfo? OSError
);
public record NotificationResponse(string? UID, EventInfo Event, long? Size, int? Tid, IEnumerable<StackInfo>? Stack, DmxInfo? DMX, ExceptionInfo? Exception);
public record EventInfo(int ID, string Name);
public record LastKnownStateResponse(
    string? UID,
    [property: JsonConverter(typeof(HmonTimestampConverter))] DateTime TS,
    ActivityInfo? Activity,
    LocationInfo? Location,
    [property: JsonPropertyName("WS FULL")] WsFullInfo? WsFull
);
public record ActivityInfo(int Code, [property: JsonConverter(typeof(HmonTimestampConverter))] DateTime TS);
public record LocationInfo(string Function, int Line, [property: JsonConverter(typeof(HmonTimestampConverter))] DateTime TS);
public record WsFullInfo([property: JsonConverter(typeof(HmonTimestampConverter))] DateTime TS);
public class HmonTimestampConverter : JsonConverter<DateTime>
{
  private const string Format = "yyyyMMdd'T'HHmmss.fff'Z'";
  public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    var str = reader.GetString();
    return str is null
      ? throw new JsonException("Timestamp string is null")
      : DateTime.ParseExact(str, Format, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
  }
  public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
  {
    writer.WriteStringValue(value.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture));
  }
}
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
public record InternalLocationInfo(string File, int Line);
public class InternalLocationInfoConverter : JsonConverter<InternalLocationInfo?>
{
  public override InternalLocationInfo? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    if (reader.TokenType != JsonTokenType.StartArray)
      return null;
    reader.Read();
    var file = reader.GetString();
    reader.Read();
    var line = reader.GetInt32();
    reader.Read(); // EndArray
    return new InternalLocationInfo(file!, line);
  }
  public override void Write(Utf8JsonWriter writer, InternalLocationInfo? value, JsonSerializerOptions options)
  {
    if (value == null) {
      writer.WriteNullValue();
      return;
    }
    writer.WriteStartArray();
    writer.WriteStringValue(value.File);
    writer.WriteNumberValue(value.Line);
    writer.WriteEndArray();
  }
}
// OSErrorInfo: represents a 3-element tuple (int, int, string) from protocol
public record OSErrorInfo(int Source, int Code, string Description);
public class OSErrorInfoJsonConverter : JsonConverter<OSErrorInfo?>
{
  public override OSErrorInfo? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    if (reader.TokenType == JsonTokenType.Null)
      return null;
    if (reader.TokenType != JsonTokenType.StartArray)
      throw new JsonException("OSError must be a 3-element array");
    reader.Read();
    int source = reader.GetInt32();
    reader.Read();
    int code = reader.GetInt32();
    reader.Read();
    string? desc = reader.GetString();
    reader.Read(); // EndArray
    return new OSErrorInfo(source, code, desc ?? "");
  }
  public override void Write(Utf8JsonWriter writer, OSErrorInfo? value, JsonSerializerOptions options)
  {
    if (value == null) {
      writer.WriteNullValue();
      return;
    }
    writer.WriteStartArray();
    writer.WriteNumberValue(value.Source);
    writer.WriteNumberValue(value.Code);
    writer.WriteStringValue(value.Description);
    writer.WriteEndArray();
  }
}
