using System.Text.Json.Serialization;

namespace Dyalog.Hmon.Client.Lib;

[JsonSerializable(typeof(HmonEvent))]
[JsonSerializable(typeof(FactsResponse))]
[JsonSerializable(typeof(NotificationResponse))]
[JsonSerializable(typeof(LastKnownStateResponse))]
[JsonSerializable(typeof(SubscribedResponse))]
[JsonSerializable(typeof(RideConnectionResponse))]
[JsonSerializable(typeof(UserMessageResponse))]
[JsonSerializable(typeof(UnknownCommandResponse))]
[JsonSerializable(typeof(MalformedCommandResponse))]
[JsonSerializable(typeof(InvalidSyntaxResponse))]
[JsonSerializable(typeof(DisallowedUidResponse))]
[JsonSerializable(typeof(Fact))] // Added for polymorphic Fact support
internal partial class HmonJsonContext : JsonSerializerContext
{
}
