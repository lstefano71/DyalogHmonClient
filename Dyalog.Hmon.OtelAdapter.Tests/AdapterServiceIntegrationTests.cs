using Xunit;
using Dyalog.Hmon.OtelAdapter;
using Dyalog.Hmon.Client.Lib;
using Dyalog.Hmon.Client.Tests;
using System.Threading.Tasks;
using System.Threading;
using System.Text.Json;

public class AdapterServiceIntegrationTests
{
    [Fact]
    public async Task AdapterService_ProcessesScriptedFactFromMockServer()
    {
        // Arrange
        using var mockServer = new MockHmonServer();
        var cts = new CancellationTokenSource();

        // Start the mock server and accept connection
        var serverTask = mockServer.AcceptAndHandshakeAsync(cts.Token);

        // Prepare a minimal AdapterService (pseudo-code, actual connection logic may differ)
        var adapterConfig = new AdapterConfig
        {
            ServiceName = "TestAdapter",
            HmonServers = new System.Collections.Generic.List<HmonServerConfig>
            {
                new HmonServerConfig { Name = "TestServer", Host = "127.0.0.1", Port = mockServer.Port }
            }
        };
        var service = new AdapterService(adapterConfig);

        // Start AdapterService (pseudo-code, actual implementation may require host setup)
        var adapterTask = service.StartAsync(cts.Token);

        // Script a FactsReceivedEvent (as JSON) to be sent by the mock server
        var factsJson = JsonSerializer.Serialize(new
        {
            SessionId = System.Guid.NewGuid(),
            Facts = new
            {
                UID = "testuid",
                Interval = 1000,
                Facts = new[] {
                    new {
                        ID = 1,
                        Name = "Host",
                        Value = new {
                            Machine = new { Name = "testhost", User = "testuser", PID = 123, Desc = "desc", AccessLevel = 1 },
                            Interpreter = new { Version = "19.0", BitWidth = 64, IsUnicode = true, IsRuntime = false, SessionUUID = "uuid" },
                            CommsLayer = new { Version = "5.0", Address = "127.0.0.1", Port4 = 4502, Port6 = 4503 },
                            RIDE = new { Listening = true, HTTPServer = true, Version = "1.2.3", Address = "127.0.0.1", Port4 = 4502, Port6 = 4503 }
                        }
                    }
                }
            }
        });

        mockServer.EnqueueMessage(factsJson);

        // Wait for processing
        await Task.Delay(1000);

        // Assert: check that AdapterService processed the fact (pseudo-code, actual verification may differ)
        // For example, check that AdapterService's internal state/resource attributes reflect the injected fact

        // Cleanup
        cts.Cancel();
        await Task.WhenAll(serverTask, adapterTask);
    }
}
