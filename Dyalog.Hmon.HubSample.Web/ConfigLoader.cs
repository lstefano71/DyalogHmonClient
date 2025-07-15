using System.Text.Json;

namespace Dyalog.Hmon.HubSample.Web;

public static class ConfigLoader
{
  // Cache the JsonSerializerOptions instance
  private static readonly JsonSerializerOptions CachedSerializerOptions = new() {
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true
  };

  public static async Task<HubSampleConfig> LoadAsync(string[] args)
  {
    string? configPath = null;

    // 1. CLI argument: --config path/to/config.json
    for (int i = 0; i < args.Length - 1; i++) {
      if (args[i] == "--config") {
        configPath = args[i + 1];
        break;
      }
    }

    // 2. Environment variable
    configPath ??= Environment.GetEnvironmentVariable(HubSampleConfig.EnvVarName);

    // 3. Default file
    if (string.IsNullOrWhiteSpace(configPath)) {
      configPath = HubSampleConfig.DefaultConfigFileName;
    }

    // Try current directory, then project directory
    if (!File.Exists(configPath)) {
      var projectDir = Path.Combine(AppContext.BaseDirectory, configPath);
      if (File.Exists(projectDir))
        configPath = projectDir;
      else
        throw new FileNotFoundException($"Config file not found: {configPath}");
    }

    await using FileStream openStream = File.OpenRead(configPath);

    // Use the cached serializer options
    var config = await JsonSerializer.DeserializeAsync<HubSampleConfig>(openStream, CachedSerializerOptions) ?? throw new InvalidOperationException("Failed to deserialize configuration.");

    // Ensure HmonServers is never null
    if (config.HmonServers == null)
      config = config with { HmonServers = [] };

    // Ensure PollFacts is never null or empty, default to ["host", "threads"]
    if (config.PollFacts == null || config.PollFacts.Count == 0)
      config = config with { PollFacts = ["host", "threads"] };

    // Ensure PollIntervalSeconds is set, default to 5
    if (config.PollIntervalSeconds == null || config.PollIntervalSeconds <= 0)
      config = config with { PollIntervalSeconds = 5 };

    // Ensure EventSubscription is set, default to ["UntrappedSignal"]
    if (config.EventSubscription == null || config.EventSubscription.Count == 0)
      config = config with { EventSubscription = ["UntrappedSignal"] };

    // Ensure EventHistorySize is set, default to 10
    if (config.EventHistorySize == null || config.EventHistorySize <= 0)
      config = config with { EventHistorySize = 10 };

    return config;
  }
}
