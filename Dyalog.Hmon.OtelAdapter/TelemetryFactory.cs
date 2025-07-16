using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace Dyalog.Hmon.OtelAdapter;

/// <summary>
/// Factory for configuring and providing OpenTelemetry MeterProvider for metrics export.
/// </summary>
public class TelemetryFactory
{
  public MeterProvider MeterProvider { get; }

  /// <summary>
  /// Initializes a new instance of the <see cref="TelemetryFactory"/> class and configures the MeterProvider.
  /// </summary>
  /// <param name="config">Adapter configuration containing OTEL exporter and meter settings.</param>
  public TelemetryFactory(AdapterConfig config)
  {
    var resourceBuilder = ResourceBuilder.CreateDefault()
        .AddService(config.ServiceName);

    MeterProvider = Sdk.CreateMeterProviderBuilder()
        .SetResourceBuilder(resourceBuilder)
        .AddMeter(config.MeterName)
        .AddOtlpExporter(options => {
          options.Endpoint = new Uri(config.OtelExporter.Endpoint);
          if (!string.IsNullOrWhiteSpace(config.OtelExporter.Protocol))
            options.Protocol = Enum.TryParse<OpenTelemetry.Exporter.OtlpExportProtocol>(config.OtelExporter.Protocol, true, out var proto)
                    ? proto
                    : OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
        })
        .Build();
  }
}
