using System.ComponentModel.DataAnnotations;

namespace Dyalog.Hmon.OtelAdapter;

public record AdapterConfig
{
  [Required]
  public string ServiceName { get; init; } = "HMON-to-OTEL Adapter";

  [Required]
  public List<HmonServerConfig> HmonServers { get; init; } = [];

  [Required]
  public OtelExporterConfig OtelExporter { get; init; } = new();

  public string? LogLevel { get; init; } = "Information";

  /// <summary>
  /// Polling interval for facts in milliseconds. Default: 1000ms.
  /// </summary>
  public int PollingIntervalMs { get; init; } = 1000;
}

public record HmonServerConfig
{
  [Required]
  public string Host { get; init; } = "";

  [Required]
  public int Port { get; init; }

  public string? Name { get; init; }
  public bool UseWebSocket { get; init; } = false;
}

public record OtelExporterConfig
{
  [Required]
  public string Endpoint { get; init; } = "";

  public string? Protocol { get; init; }
  public string? ApiKey { get; init; }
}
