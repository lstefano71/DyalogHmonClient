using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Dyalog.Hmon.HubSample.Web;

public record HmonServerConfig(
    [Required] string Name,
    [Required] string Host,
    [Range(1, 65535)] int Port
);

public record PollListenerConfig(
    [Required] string Ip,
    [Range(1, 65535)] int Port
);

public record ApiConfig(
    [Required] string Ip,
    [Range(1, 65535)] int Port
);


public record HubSampleConfig
{
  [JsonPropertyName("hmonServers")]
  public List<HmonServerConfig>? HmonServers { get; init; } = [];

  [JsonPropertyName("pollListener")]
  public PollListenerConfig? PollListener { get; init; }

  [JsonPropertyName("api")]
  public ApiConfig? Api { get; init; }

  [JsonPropertyName("autoShutdownSeconds")]
  public int? AutoShutdownSeconds { get; init; }

  [JsonPropertyName("logLevel")]
  public string? LogLevel { get; init; }

  [JsonPropertyName("pollFacts")]
  public List<string>? PollFacts { get; init; }

  [JsonPropertyName("pollIntervalSeconds")]
  public int? PollIntervalSeconds { get; init; }

  [JsonPropertyName("eventSubscription")]
  public List<string>? EventSubscription { get; init; }

  [JsonPropertyName("eventHistorySize")]
  public int? EventHistorySize { get; init; }

  public HubSampleConfig() { }

  public static string DefaultConfigFileName => "config.json";
  public static string EnvVarName => "HMON_HUB_CONFIG";
}
