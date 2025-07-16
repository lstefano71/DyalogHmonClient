using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using Spectre.Console;
using Dyalog.Hmon.Client.Lib;
using System.Diagnostics.Metrics;

namespace Dyalog.Hmon.OtelAdapter;

public class AdapterService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("AdapterService started.");
        AnsiConsole.MarkupLine("[bold blue]AdapterService running.[/]");

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("config.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(Environment.GetCommandLineArgs())
            .Build();

        var adapterConfig = config.Get<AdapterConfig>();
        var validationContext = new ValidationContext(adapterConfig ?? new AdapterConfig());
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        if (adapterConfig == null || !Validator.TryValidateObject(adapterConfig, validationContext, validationResults, true))
        {
            Log.Error("Configuration validation failed: {Errors}", string.Join("; ", validationResults.Select(r => r.ErrorMessage)));
            AnsiConsole.MarkupLine("[bold red]Configuration validation failed.[/]");
            return;
        }

        Log.Debug("Loaded configuration: {@AdapterConfig}", adapterConfig);

        // Instantiate HMON orchestrator
        var orchestrator = new Dyalog.Hmon.Client.Lib.HmonOrchestrator();

        // Instantiate TelemetryFactory for OTEL metrics export
        var telemetryFactory = new TelemetryFactory(adapterConfig);

        // Connect to HMON interpreters
        foreach (var server in adapterConfig.HmonServers)
        {
            try
            {
                orchestrator.AddServer(server.Host, server.Port, server.Name);
                Log.Information("Added HMON server: {Host}:{Port} ({Name})", server.Host, server.Port, server.Name ?? "");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to add HMON server: {Host}:{Port}", server.Host, server.Port);
            }
        }

        // TODO: Optionally start listener if required by config

        // Metric instrument cache
        var meter = new Meter("HMON");
        var instrumentCache = new Dictionary<string, object>();

        // Subscribe to client disconnects
        orchestrator.ClientDisconnected += async args =>
        {
            Log.Error("Client disconnected: {Host}:{Port} ({Name}) Reason={Reason}", args.Host, args.Port, args.FriendlyName ?? "", args.Reason);

            // Adapter-generated lifecycle log: include OTEL attributes
            var otelAttributes = new List<KeyValuePair<string, object>>
            {
                new("net.peer.name", args.Host),
                new("net.peer.port", args.Port),
                new("service.name", adapterConfig.ServiceName),
                new("error.message", args.Reason),
                new("session.id", args.SessionId.ToString())
            };
            Log.Error("OTEL lifecycle log: ClientDisconnected [{Attributes}]", string.Join(",", otelAttributes.Select(a => $"{a.Key}={a.Value}")));

            await Task.CompletedTask;
        };

        // Main event processing loop
        await foreach (var hmonEvent in orchestrator.Events.WithCancellation(stoppingToken))
        {
            switch (hmonEvent)
            {
                case FactsReceivedEvent factsEvent:
                    Log.Debug("Processing FactsReceivedEvent with {FactCount} facts.", factsEvent.Facts.Facts.Count());
                    foreach (var fact in factsEvent.Facts.Facts)
                    {
                        Log.Debug("Mapping fact: {FactType}", fact.GetType().Name);

                        // Example: HostFact mapped to "host.pid" gauge metric
                        if (fact is HostFact hostFact)
                        {
                            var metricName = "host.pid";
                            var value = hostFact.Machine.PID;
                            var tags = new List<KeyValuePair<string, object>>
                            {
                                new("host", hostFact.Machine.Name ?? ""),
                                new("user", hostFact.Machine.User ?? "")
                            };

                            // Create or retrieve instrument
                            if (!instrumentCache.TryGetValue(metricName, out var instrument))
                            {
                                instrument = meter.CreateObservableGauge<int>(metricName, () => value, description: "Host PID");
                                instrumentCache[metricName] = instrument;
                            }

                            Log.Information("Recording metric {MetricName}: {Value} [{Tags}]", metricName, value, string.Join(",", tags.Select(t => $"{t.Key}={t.Value}")));
                        }
                        // WorkspaceFact mapped to multiple workspace metrics
                        else if (fact is WorkspaceFact wsFact)
                        {
                            var workspaceMetrics = new[]
                            {
                                ("workspace.available", wsFact.Available, "Workspace Available"),
                                ("workspace.used", wsFact.Used, "Workspace Used"),
                                ("workspace.compactions", wsFact.Compactions, "Workspace Compactions"),
                                ("workspace.garbage_collections", wsFact.GarbageCollections, "Workspace Garbage Collections"),
                                ("workspace.garbage_pockets", wsFact.GarbagePockets, "Workspace Garbage Pockets"),
                                ("workspace.free_pockets", wsFact.FreePockets, "Workspace Free Pockets"),
                                ("workspace.used_pockets", wsFact.UsedPockets, "Workspace Used Pockets")
                            };
                            foreach (var (metricName, value, description) in workspaceMetrics)
                            {
                                var tags = new List<KeyValuePair<string, object>>
                                {
                                    new("wsid", wsFact.WSID ?? "")
                                };

                                if (!instrumentCache.TryGetValue(metricName, out var instrument))
                                {
                                    instrument = meter.CreateObservableGauge<long>(metricName, () => value, description: description);
                                    instrumentCache[metricName] = instrument;
                                }

                                Log.Information("Recording metric {MetricName}: {Value} [{Tags}]", metricName, value, string.Join(",", tags.Select(t => $"{t.Key}={t.Value}")));
                            }
                        }
                        // AccountInformationFact mapped to compute/connect/keying time metrics
                        else if (fact is AccountInformationFact accFact)
                        {
                            var metrics = new[]
                            {
                                ("account.compute_time", accFact.ComputeTime, "Compute Time"),
                                ("account.connect_time", accFact.ConnectTime, "Connect Time"),
                                ("account.keying_time", accFact.KeyingTime, "Keying Time")
                            };
                            foreach (var (metricName, value, description) in metrics)
                            {
                                var tags = new List<KeyValuePair<string, object>>
                                {
                                    new("user_id", accFact.UserIdentification)
                                };
                                if (!instrumentCache.TryGetValue(metricName, out var instrument))
                                {
                                    instrument = meter.CreateObservableGauge<long>(metricName, () => value, description: description);
                                    instrumentCache[metricName] = instrument;
                                }
                                Log.Information("Recording metric {MetricName}: {Value} [{Tags}]", metricName, value, string.Join(",", tags.Select(t => $"{t.Key}={t.Value}")));
                            }
                        }
                        // InterpreterInfo metrics (from HostFact.Interpreter)
                        else if (fact is HostFact hostFact2)
                        {
                            var interp = hostFact2.Interpreter;
                            var metricName = "interpreter.version";
                            var tags = new List<KeyValuePair<string, object>>
                            {
                                new("version", interp.Version ?? "")
                            };
                            // No direct value, but can record as an attribute/tag
                            Log.Information("Interpreter version: {Version} [{Tags}]", interp.Version, string.Join(",", tags.Select(t => $"{t.Key}={t.Value}")));
                        }
                        // CommsLayerInfo metrics (from HostFact.CommsLayer)
                        else if (fact is HostFact hostFact3 && hostFact3.CommsLayer is not null)
                        {
                            var comms = hostFact3.CommsLayer;
                            var metrics = new[]
                            {
                                ("commslayer.port4", comms.Port4, "CommsLayer IPv4 Port"),
                                ("commslayer.port6", comms.Port6, "CommsLayer IPv6 Port")
                            };
                            foreach (var (metricName, value, description) in metrics)
                            {
                                var tags = new List<KeyValuePair<string, object>>
                                {
                                    new("address", comms.Address ?? ""),
                                    new("version", comms.Version ?? "")
                                };
                                if (!instrumentCache.TryGetValue(metricName, out var instrument))
                                {
                                    instrument = meter.CreateObservableGauge<int>(metricName, () => value, description: description);
                                    instrumentCache[metricName] = instrument;
                                }
                                Log.Information("Recording metric {MetricName}: {Value} [{Tags}]", metricName, value, string.Join(",", tags.Select(t => $"{t.Key}={t.Value}")));
                            }
                        }
                        // RideInfo metrics (from HostFact.RIDE)
                        else if (fact is HostFact hostFact4 && hostFact4.RIDE is not null)
                        {
                            var ride = hostFact4.RIDE;
                            var metricName = "ride.listening";
                            var value = ride.Listening ? 1 : 0;
                            var tags = new List<KeyValuePair<string, object>>
                            {
                                new("ride", "listening")
                            };
                            if (!instrumentCache.TryGetValue(metricName, out var instrument))
                            {
                                instrument = meter.CreateObservableGauge<int>(metricName, () => value, description: "RIDE Listening");
                                instrumentCache[metricName] = instrument;
                            }
                            Log.Information("Recording metric {MetricName}: {Value} [{Tags}]", metricName, value, string.Join(",", tags.Select(t => $"{t.Key}={t.Value}")));
                        }
                        // TODO: Add more Fact type mappings as per PRD
                    }
                    break;
                // Log mapping: NotificationReceivedEvent
                case NotificationReceivedEvent notificationEvent:
                    var notif = notificationEvent.Notification;
                    var notifType = notif.Event.Name;
                    var notifTags = new List<KeyValuePair<string, object>>
                    {
                        new("uid", notif.UID ?? ""),
                        new("event", notifType)
                    };
                    if (notifType == "UntrappedSignal" || notifType == "TrappedSignal")
                    {
                        // Severity: ERROR or WARN
                        var severity = notifType == "UntrappedSignal" ? "ERROR" : "WARN";
                        Log.Error("Signal notification: {Event} [{Tags}]", notifType, string.Join(",", notifTags.Select(t => $"{t.Key}={t.Value}")));
                        // Optionally fetch ThreadsFact for Tid and log DMX/Stack/ThreadInfo
                        if (notif.Tid.HasValue)
                        {
                            Log.Debug("Fetching ThreadsFact for Tid={Tid}", notif.Tid.Value);
                            // TODO: orchestrator.GetFactsAsync for ThreadsFact and log DMX/Stack/ThreadInfo
                        }
                    }
                    else if (notifType == "WorkspaceResize")
                    {
                        Log.Information("WorkspaceResize notification: {Event} [{Tags}]", notifType, string.Join(",", notifTags.Select(t => $"{t.Key}={t.Value}")));
                    }
                    else
                    {
                        Log.Information("Notification: {Event} [{Tags}]", notifType, string.Join(",", notifTags.Select(t => $"{t.Key}={t.Value}")));
                    }
                    break;
                // Log mapping: UserMessageReceivedEvent
                case UserMessageReceivedEvent userMsgEvent:
                    var msg = userMsgEvent.Message;
                    Log.Information("User message: UID={UID}, Message={Message}", msg.UID, msg.Message.ToString());
                    break;
                // ClientDisconnectedEventArgs is not a HmonEvent; handle disconnects via orchestrator event/callback elsewhere.
                default:
                    Log.Debug("Received HMON event: {EventType}", hmonEvent.GetType().Name);
                    break;
            }
        }

        Log.Information("AdapterService stopping.");
    }
}
