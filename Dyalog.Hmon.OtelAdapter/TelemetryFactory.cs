using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;

namespace Dyalog.Hmon.OtelAdapter;

public class TelemetryFactory
{
    public MeterProvider MeterProvider { get; }

    public TelemetryFactory(AdapterConfig config)
    {
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(config.ServiceName);

        MeterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            // TODO: Add metric instruments
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(config.OtelExporter.Endpoint);
                if (!string.IsNullOrWhiteSpace(config.OtelExporter.Protocol))
                    options.Protocol = Enum.TryParse<OpenTelemetry.Exporter.OtlpExportProtocol>(config.OtelExporter.Protocol, true, out var proto)
                        ? proto
                        : OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
            })
            .Build();
    }
}
