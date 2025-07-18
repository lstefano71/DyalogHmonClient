using Microsoft.Extensions.Options;

using System.ComponentModel.DataAnnotations;

namespace Dyalog.Hmon.HubSample.Web;

/// <summary>
/// Validates configuration options for the hub sample web application.
/// </summary>
public class HubSampleConfigValidator : IValidateOptions<HubSampleConfig>
{
  public ValidateOptionsResult Validate(string? name, HubSampleConfig config)
  {
    var validationResults = new List<ValidationResult>();
    var context = new ValidationContext(config, null, null);

    // DataAnnotations validation
    if (!Validator.TryValidateObject(config, context, validationResults, true)) {
      var errors = validationResults.Select(r => r.ErrorMessage ?? "Unknown error").ToArray();
      return ValidateOptionsResult.Fail(errors);
    }

    // Custom: at least one of hmonServers or pollListener must be present
    bool hasHmonServers = config.HmonServers is { Count: > 0 };
    bool hasPollListener = config.PollListener is not null;
    if (!hasHmonServers && !hasPollListener)
      return ValidateOptionsResult.Fail("At least one of 'hmonServers' or 'pollListener' must be present.");

    // Custom: api is required
    return config.Api is null ? ValidateOptionsResult.Fail("'api' section is required.") : ValidateOptionsResult.Success;
  }
}
