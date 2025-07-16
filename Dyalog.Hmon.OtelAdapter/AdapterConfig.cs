using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Dyalog.Hmon.OtelAdapter;

public record AdapterConfig
{
    [Required]
    public string ServiceName { get; init; } = "HMON-to-OTEL Adapter";

    [Required]
    public List<HmonServerConfig> HmonServers { get; init; } = new();

    [Required]
    public OtelExporterConfig OtelExporter { get; init; } = new();

    public string? LogLevel { get; init; } = "Information";
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
