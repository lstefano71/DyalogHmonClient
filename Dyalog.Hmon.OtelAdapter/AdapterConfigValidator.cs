using Microsoft.Extensions.Options;

using System.ComponentModel.DataAnnotations;

namespace Dyalog.Hmon.OtelAdapter;

public class AdapterConfigValidator : IValidateOptions<AdapterConfig>
{
  public ValidateOptionsResult Validate(string? name, AdapterConfig config)
  {
    var validationResults = new List<ValidationResult>();
    var context = new ValidationContext(config, null, null);

    // DataAnnotations validation
    if (!Validator.TryValidateObject(config, context, validationResults, true)) {
      var errors = validationResults.Select(r => r.ErrorMessage ?? "Unknown error").ToArray();
      return ValidateOptionsResult.Fail(errors);
    }

    // Custom: at least one of HmonServers or PollListener must be present
    bool hasHmonServers = config.HmonServers is { Count: > 0 };
    bool hasPollListener = config.PollListener is not null;
    if (!hasHmonServers && !hasPollListener)
      return ValidateOptionsResult.Fail("At least one of 'HmonServers' or 'PollListener' must be present.");

    // Custom: OtelExporter.Endpoint must be a non-empty string
    if (string.IsNullOrWhiteSpace(config.OtelExporter?.Endpoint))
      return ValidateOptionsResult.Fail("'OtelExporter.Endpoint' is required and must be a non-empty string.");

    // Custom: PollingIntervalMs must be positive
    if (config.PollingIntervalMs <= 0)
      return ValidateOptionsResult.Fail("'PollingIntervalMs' must be a positive integer.");

    // Custom: Each HmonServerConfig must have valid Host
    foreach (var server in config.HmonServers) {
      if (string.IsNullOrWhiteSpace(server.Host))
        return ValidateOptionsResult.Fail("Each 'HmonServerConfig.Host' must be a non-empty string.");
    }

    return ValidateOptionsResult.Success;
  }
}
