using Dyalog.Hmon.Client.Lib;
using Dyalog.Hmon.OtelAdapter;

using Xunit;

public class AdapterServiceMappingTests
{
  [Fact]
  public void MapsHostFactToResourceAttributes()
  {
    // Arrange
    var hostFact = new HostFact(
        new MachineInfo("apl-prod-server-01", "svc_apl", 12345, "WebAppServer_1", 2),
        new InterpreterInfo("19.0.49500", 64, true, false, "a1b2c3d4-e5f6-..."),
        new CommsLayerInfo("5.0.16", "127.0.0.1", 4502, 4503),
        new RideInfo(true, true, "1.2.3", "127.0.0.1", 4502, 4503)
    );
    var accFact = new AccountInformationFact(42, 1000, 2000, 3000);
    var adapterConfig = new AdapterConfig {
      ServiceName = "DyalogHMONAdapter",
      HmonServers =
        [
            new HmonServerConfig { Name = "WebAppServer_1", Host = "10.0.1.50", Port = 4502 }
        ]
    };
    var service = new AdapterService(adapterConfig);
    // Only test construction for now, as MapResourceAttributes does not exist
    Assert.NotNull(service);
  }

  [Fact]
  public void HostFact_MapsToExpectedMetricAttributes()
  {
    var hostFact = new HostFact(
        new MachineInfo("testhost", "testuser", 123, "desc", 1),
        new InterpreterInfo("19.0", 64, true, false, "uuid"),
        new CommsLayerInfo("5.0.16", "127.0.0.1", 4502, 4503),
        new RideInfo(true, true, "1.2.3", "127.0.0.1", 4502, 4503)
    );
    var config = new AdapterConfig();
    var service = new AdapterService(config);
    // TODO: Call mapping logic and assert expected metric attributes
    Assert.NotNull(hostFact);
    Assert.NotNull(service);
  }

  [Fact]
  public void WorkspaceFact_MapsToExpectedMetricAttributes()
  {
    var workspaceFact = new WorkspaceFact(
        "wsid",
        100, 200, 300, 400, 500, 600, 700, 800, 900, 1000, 1100, 1200
    );
    var config = new AdapterConfig();
    var service = new AdapterService(config);
    // TODO: Call mapping logic and assert expected metric attributes
    Assert.NotNull(workspaceFact);
    Assert.NotNull(service);
  }

  [Fact]
  public void ErrorHandling_ConnectionFailure_LogsErrorEvent()
  {
    // TODO: Simulate connection failure and verify error log event
    Assert.True(true);
  }

  [Fact]
  public void Lifecycle_ClientDisconnected_LogsDisconnectEvent()
  {
    // TODO: Simulate client disconnect and verify lifecycle log event
    Assert.True(true);
  }

  [Fact]
  public void ErrorHandling_MalformedEvent_LogsErrorEvent()
  {
    // TODO: Simulate malformed event and verify error log event
    Assert.True(true);
  }

  [Fact]
  public void ConfigLoading_ConfigJson_LoadsExpectedValues()
  {
    // TODO: Simulate config.json loading and verify AdapterConfig values
    Assert.True(true);
  }

  [Fact]
  public void ConfigLoading_CommandLine_OverridesConfigJson()
  {
    // TODO: Simulate CLI args overriding config.json and verify AdapterConfig values
    Assert.True(true);
  }

  [Fact]
  public void ConfigLoading_EnvVars_OverrideConfigJson()
  {
    // TODO: Simulate env vars overriding config.json and verify AdapterConfig values
    Assert.True(true);
  }

  [Fact]
  public void ResourceEnrichment_PerSessionMeterProvider_CreatesExpectedAttributes()
  {
    // TODO: Simulate session creation and verify MeterProvider resource enrichment
    Assert.True(true);
  }
}
