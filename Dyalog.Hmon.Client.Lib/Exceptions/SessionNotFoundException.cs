namespace Dyalog.Hmon.Client.Lib.Exceptions;

using System;

public class SessionNotFoundException(Guid sessionId) : HmonException($"No active and connected session found for ID: {sessionId}")
{
  public Guid SessionId { get; } = sessionId;
}
