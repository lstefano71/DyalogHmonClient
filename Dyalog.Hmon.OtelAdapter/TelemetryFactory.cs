using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace Dyalog.Hmon.OtelAdapter;

public class TelemetryFactory
{
  public MeterProvider MeterProvider { get; }
  // Note: OpenTelemetry .NET does not provide a direct LoggerProvider builder.
  // Log export should be configured via Serilog or Microsoft.Extensions.Logging with OpenTelemetry extensions.

  public TelemetryFactory(AdapterConfig config)
  {
    var resourceBuilder = ResourceBuilder.CreateDefault()
        .AddService(config.ServiceName);

    MeterProvider = Sdk.CreateMeterProviderBuilder()
        .SetResourceBuilder(resourceBuilder)
        // TODO: Add metric instruments
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
