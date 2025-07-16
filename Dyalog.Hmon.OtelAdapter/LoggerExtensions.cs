using Microsoft.Extensions.Logging;

// Choose a namespace that makes sense for your project
namespace Dyalog.Hmon.OtelAdapter.Logging
{
  /// <summary>
  /// Provides extension methods for Microsoft.Extensions.Logging.ILogger to log
  /// a single message with a temporary scope.
  /// </summary>
  [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "<Pending>")]
  public static class ScopedLoggerExtensions
  {
    // --- Core Method ---

    /// <summary>
    /// Logs a message with a given log level, temporarily applying a scope from the provided dictionary.
    /// This scope is only active for the duration of this single log call.
    /// </summary>
    /// <param name="logger">The ILogger instance.</param>
    /// <param name="logLevel">The severity level of the log message.</param>
    /// <param name="contextProperties">A dictionary of properties to add to the log's scope.</param>
    /// <param name="message">The message template.</param>
    /// <param name="args">Arguments for the message template.</param>
    public static void LogWithContext(
        this ILogger logger,
        LogLevel logLevel,
        IDictionary<string, object> contextProperties,
        string message,
        params object[] args)
    {
      using var scope = logger.BeginScope(contextProperties);
      logger.Log(logLevel, message, args);
    }

    // --- Helper Methods for Each Log Level ---

    public static void LogInformationWithContext(
        this ILogger logger,
        IDictionary<string, object> contextProperties,
        string message,
        params object[] args)
    {
      logger.LogWithContext(LogLevel.Information, contextProperties, message, args);
    }

    public static void LogWarningWithContext(
        this ILogger logger,
        IDictionary<string, object> contextProperties,
        string message,
        params object[] args)
    {
      logger.LogWithContext(LogLevel.Warning, contextProperties, message, args);
    }

    public static void LogDebugWithContext(
        this ILogger logger,
        IDictionary<string, object> contextProperties,
        string message,
        params object[] args)
    {
      logger.LogWithContext(LogLevel.Debug, contextProperties, message, args);
    }

    public static void LogTraceWithContext(
        this ILogger logger,
        IDictionary<string, object> contextProperties,
        string message,
        params object[] args)
    {
      logger.LogWithContext(LogLevel.Trace, contextProperties, message, args);
    }

    // --- Overloads for Handling Exceptions ---

    public static void LogErrorWithContext(
        this ILogger logger,
        Exception exception,
        IDictionary<string, object> contextProperties,
        string message,
        params object[] args)
    {
      using var scope = logger.BeginScope(contextProperties);
      logger.LogError(exception, message, args);
    }

    public static void LogErrorWithContext(
        this ILogger logger,
        IDictionary<string, object> contextProperties,
        string message,
        params object[] args)
    {
      logger.LogWithContext(LogLevel.Error, contextProperties, message, args);
    }

    public static void LogCriticalWithContext(
        this ILogger logger,
        Exception exception,
        IDictionary<string, object> contextProperties,
        string message,
        params object[] args)
    {
      using var scope = logger.BeginScope(contextProperties);
      logger.LogCritical(exception, message, args);
    }

    public static void LogCriticalWithContext(
        this ILogger logger,
        IDictionary<string, object> contextProperties,
        string message,
        params object[] args)
    {
      logger.LogWithContext(LogLevel.Critical, contextProperties, message, args);
    }
  }
}