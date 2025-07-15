namespace Dyalog.Hmon.HubSample.Web;

public record HmonServerConfig(
    string Name,
    string Host,
    int Port
);

public record PollListenerConfig(
    string Ip,
    int Port
);

public record ApiConfig(
    string Ip,
    int Port
);

public record HubSampleConfig(
    List<HmonServerConfig> HmonServers,
    PollListenerConfig? PollListener,
    ApiConfig Api,
    int? AutoShutdownSeconds = null,
    string? LogLevel = null,
    List<string>? PollFacts = null,
    int? PollIntervalSeconds = null,
    List<string>? EventSubscription = null,
    int? EventHistorySize = null
)
{
  public static string DefaultConfigFileName => "config.json";
  public static string EnvVarName => "HMON_HUB_CONFIG";
}
