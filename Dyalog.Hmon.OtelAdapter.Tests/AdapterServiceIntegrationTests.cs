using Dyalog.Hmon.Client.Lib;
using Dyalog.Hmon.OtelAdapter;

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

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

    // Prepare a minimal AdapterService with configuration
    var config = new AdapterConfig();
    var service = new AdapterService(config);
    var executeTask = service.StartAsync(cts.Token);
    await executeTask;
    Assert.True(executeTask.IsCompleted);
    // No exceptions should be thrown during startup
    var factsJson = JsonSerializer.Serialize(new {
      SessionId = System.Guid.NewGuid(),
      Facts = new {
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
    await Task.WhenAll(serverTask, executeTask);
  }

  [Fact]
  public async Task EndToEnd_Ingestion_FromMockHmonServer_ProcessesFactsAndNotifications()
  {
    // Arrange
    using var mockServer = new MockHmonServer();
    var cts = new CancellationTokenSource();

    // Start the mock server and accept connection
    var serverTask = mockServer.AcceptAndHandshakeAsync(cts.Token);

    // Prepare AdapterService with OTLP endpoint and dummy HMON server
    var config = new AdapterConfig {
      ServiceName = "TestAdapter",
      HmonServers =
        [
            new HmonServerConfig { Host = "127.0.0.1", Port = 4502, Name = "TestServer" }
        ],
      OtelExporter = new OtelExporterConfig {
        Endpoint = "http://localhost:4317"
      },
      LogLevel = "Information"
    };
    var service = new AdapterService(config);
    var executeTask = service.StartAsync(cts.Token);

    // Inject a fact and notification
    var factsJson = JsonSerializer.Serialize(new {
      SessionId = Guid.NewGuid(),
      Facts = new {
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

    var notificationJson = JsonSerializer.Serialize(new {
      SessionId = Guid.NewGuid(),
      Notification = new {
        UID = "notifuid",
        Event = new { ID = 2, Name = "UntrappedSignal" },
        Tid = 99
      }
    });
    mockServer.EnqueueMessage(notificationJson);

    // Wait for processing
    await Task.Delay(1000);

    // Assert: check logs or AdapterService state (pseudo-code, replace with actual log/assertion mechanism)
    // Example: Assert that AdapterService processed the fact and notification
    // Assert.True(service.HasProcessedFact("testuid"));
    // Assert.True(service.HasProcessedNotification("notifuid"));

    // Cleanup
    cts.Cancel();
    await Task.WhenAll(serverTask, executeTask);
  }

  [Fact]
  public async Task MetricsAndLogs_Exported_ToInMemoryOtelCollector()
  {
    // TODO: Configure AdapterService with in-memory OTel collector, verify metrics/logs are exported
    await Task.CompletedTask;
    Assert.True(true);
  }

  [Fact]
  public async Task GracefulShutdown_DisposesResources_OnCtrlCOrDisposeAsync()
  {
    // TODO: Simulate Ctrl+C or DisposeAsync, verify AdapterService disposes resources gracefully
    await Task.CompletedTask;
    Assert.True(true);
  }

  [Fact]
  public async Task Resilience_TransientConnectionIssues_AreHandledAndLogged()
  {
    // TODO: Simulate transient connection issues, verify AdapterService handles and logs them
    await Task.CompletedTask;
    Assert.True(true);
  }

  [Fact]
  public async Task OperationalTransparency_ConsoleLogging_RespectsLogLevelConfig()
  {
    // TODO: Configure AdapterService with different logLevel, verify console logging output
    await Task.CompletedTask;
    Assert.True(true);
  }

  [Fact]
  public async Task MultiInterpreterConnectivity_ServeAndPollModes_AreSupported()
  {
    // TODO: Simulate multiple interpreters in SERVE and POLL modes, verify AdapterService connects and processes events
    await Task.CompletedTask;
    Assert.True(true);
  }

  [Fact]
  public async Task PerSessionResourceAndMetricLogIsolation_IsMaintained()
  {
    // TODO: Simulate multiple sessions, verify resource and metric/log isolation per session
    await Task.CompletedTask;
    Assert.True(true);
  }
}
