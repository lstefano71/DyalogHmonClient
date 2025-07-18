using Dyalog.Hmon.Client.Lib;

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
  /// Polling interval for facts in milliseconds. Default: 5000ms.
  /// </summary>
  [Range(100, 3600000)]
  public int PollingIntervalMs { get; init; } = 5000;

  /// <summary>
  /// Name for the OpenTelemetry Meter. Optional, defaults to "HMON".
  /// </summary>
  public string MeterName { get; init; } = "HMON";

  /// <summary>
  /// Optional: Polling listener configuration. If set, activates listener.
  /// </summary>
  public PollListenerConfig? PollListener { get; init; }

  /// <summary>
  /// List of events to subscribe to. If null or empty, defaults to all events.
  /// </summary>
  public List<SubscriptionEvent>? SubscribedEvents { get; set; } = [];

  /// <summary>
  /// Gets the effective list of subscribed events.
  /// If the list was provided in the configuration, that list is used.
  /// Otherwise, a default list of events is returned.
  /// </summary>
  [System.Text.Json.Serialization.JsonIgnore] // Prevents this from being serialized
  public IReadOnlyList<SubscriptionEvent> EventsToSubcribeTo =>
        SubscribedEvents is { Count: > 0 } // Check if the list from config is not null and not empty
            ? SubscribedEvents
            :
            // Otherwise, return the default list
            [
                SubscriptionEvent.WorkspaceCompaction,
                SubscriptionEvent.WorkspaceResize,
                SubscriptionEvent.UntrappedSignal,
                SubscriptionEvent.TrappedSignal,
                SubscriptionEvent.ThreadSwitch
            ];
}

public record HmonServerConfig
{
  [Required]
  public string Host { get; init; } = "";

  [Required]
  [Range(1, 65535)]
  public int Port { get; init; }

  public string? Name { get; init; }
}

public record OtelExporterConfig
{
  [Required]
  public string Endpoint { get; init; } = "";

  public string? Protocol { get; init; }
  public string? ApiKey { get; init; }
}

public record PollListenerConfig
{
  public string Ip { get; init; } = "0.0.0.0";
  [Range(1, 65535)]
  public int Port { get; init; }
}
