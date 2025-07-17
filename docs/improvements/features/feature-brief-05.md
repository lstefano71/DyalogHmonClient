# Feature Brief #5: Refactor OTEL Logging to use Serilog Enrichers

## Problem Statement

The `Dyalog.Hmon.OtelAdapter` project currently uses a custom static class, `ScopedLoggerExtensions`, to add contextual properties (like `session.id`, `host.name`) to its log records. While this works, it is not the idiomatic or most effective way to handle contextual logging with Serilog. It requires developers to remember to use the special `Log...WithContext` methods and manually pass a dictionary of properties for every log call.

## Proposed Solution

We will refactor the logging implementation to use Serilog's built-in context enrichment features, which are more powerful, maintainable, and aligned with best practices.

1. **Remove Custom Extension:** The `Dyalog.Hmon.OtelAdapter/LoggerExtensions.cs` file will be deleted.
2. **Configure LogContext:** In `Program.cs`, the Serilog `LoggerConfiguration` will be updated to include the context enricher.

    ```csharp
    // In Program.cs
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Is(...)
        .Enrich.FromLogContext() // Add this line
        .WriteTo.Console(...)
        .CreateLogger();
    ```

3. **Use `LogContext` for Scoping:** In `AdapterService.cs`, wrap the event processing logic for a single HMON event in a `using` block that pushes all relevant session properties into the `LogContext`.

    ```csharp
    // In AdapterService.cs, inside ProcessEventsAsync loop
    var sessionTags = ... // Get the tags for the session
    using (LogContext.PushProperty("SessionId", hmonEvent.SessionId))
    using (LogContext.Push(...)) // Push other relevant properties
    {
        // All logging calls within this block will be automatically enriched
        switch (hmonEvent)
        {
            case FactsReceivedEvent e:
                 // Now just call the standard logger method
                 _otelLogger.LogInformation("Processing {FactCount} facts", e.Facts.Facts.Count());
                 break;
            // ... other cases
        }
    }
    ```

    All calls to the old `_otelLogger.LogInformationWithContext(...)` will be replaced with standard `_otelLogger.LogInformation(...)` calls.

## API Changes

None. This is a purely internal refactoring of the adapter's implementation.

## Impact and Risks

* **Positive:** Aligns the project with standard Serilog practices. Logging code becomes cleaner, as the enrichment is handled automatically by the context. It removes the need for the custom extension class, reducing bespoke code and maintenance.
* **Negative:** None.

## Alternatives Considered

* **Continue using the custom extension methods:** Rejected because it's non-standard, more verbose for developers, and less flexible than using Serilog's built-in context features.
