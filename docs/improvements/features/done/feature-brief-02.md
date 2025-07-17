# Feature Brief #2: Implement Typed Exceptions for Error Handling

## Problem Statement

The `HmonOrchestrator` currently reports most errors through a generic `OnError` C# event or by throwing `InvalidOperationException`. This makes it difficult for a consumer to programmatically handle different types of failures. A consumer has to parse exception messages to understand what went wrong, which is brittle.

## Proposed Solution

Define a hierarchy of specific, public exception types in `Dyalog.Hmon.Client.Lib` and throw them from the orchestrator methods.

- `HmonException` (base class)
  - `SessionNotFoundException(Guid sessionId)`: Thrown when an API call is made with an invalid `SessionId`.
  - `HmonConnectionException(string message, Exception innerException)`: Base for connection-related errors.
    - `HmonHandshakeFailedException(string reason)`: Thrown when the DRP-T/HMON handshake fails.
  - `HmonCommandException(string message)`: Base for command-related errors.
    - `CommandTimeoutException(string commandName)`: Thrown when a command does not receive a response within a configured timeout.

The `OnError` event can be kept for logging/diagnostic purposes but should not be the primary mechanism for flow control.

## API Changes

- Methods like `GetFactsAsync`, `SubscribeAsync`, etc., will now be documented to throw these specific exceptions.
- The public API will include the new exception types.

## Impact and Risks

- **Positive:** Enables consumers to write robust, type-safe `try/catch` blocks. The API becomes more self-documenting about its failure modes.
- **Negative:** This is a behavioral breaking change for consumers who were relying solely on the `OnError` event. This should be communicated clearly in the release notes.
