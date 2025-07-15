using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dyalog.Hmon.Client.Lib;

/// <summary>
/// Converts boolean values to and from the HMon protocol's numeric representation (0 or 1).
/// </summary>
public class HMonBooleanConverter : JsonConverter<bool>
{
  public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    if (reader.TokenType == JsonTokenType.Number) {
      return reader.GetInt32() == 1;
    } else if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False) {
      return reader.GetBoolean();
    }
    throw new JsonException($"Unexpected token type: {reader.TokenType}");
  }

  public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
  {
    writer.WriteNumberValue(value ? 1 : 0);
  }
}
