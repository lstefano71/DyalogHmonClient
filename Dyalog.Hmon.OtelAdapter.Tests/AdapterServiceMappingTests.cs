using Xunit;
using Dyalog.Hmon.OtelAdapter;
using Dyalog.Hmon.Client.Lib;
using System.Collections.Generic;

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

        var adapterConfig = new AdapterConfig
        {
            ServiceName = "DyalogHMONAdapter",
            HmonServers = new List<HmonServerConfig>
            {
                new HmonServerConfig { Name = "WebAppServer_1", Host = "10.0.1.50", Port = 4502 }
            }
        };

        var service = new AdapterService();

        // Act
        var attributes = service.MapResourceAttributes(hostFact, accFact, adapterConfig);

        // Assert
        Assert.Equal("WebAppServer_1", attributes["service.name"]);
        Assert.Equal("apl-prod-server-01", attributes["host.name"]);
        Assert.Equal(12345, attributes["process.pid"]);
        Assert.Equal("svc_apl", attributes["process.owner"]);
        Assert.Equal(42, attributes["enduser.id"]);
        Assert.Equal("19.0.49500", attributes["service.version"]);
        Assert.Equal(64, attributes["dyalog.interpreter.bitwidth"]);
        Assert.Equal(true, attributes["dyalog.interpreter.is_unicode"]);
        Assert.Equal(false, attributes["dyalog.interpreter.is_runtime"]);
        Assert.Equal("a1b2c3d4-e5f6-...", attributes["service.instance.id"]);
        Assert.Equal(2, attributes["dyalog.hmon.access_level"]);
        Assert.Equal(true, attributes["dyalog.ride.listening"]);
        Assert.Equal("127.0.0.1", attributes["dyalog.ride.address"]);
        Assert.Equal("5.0.16", attributes["dyalog.conga.version"]);
    }
}
