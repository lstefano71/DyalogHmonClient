namespace Dyalog.Hmon.Client.Lib.Exceptions;

using System;

public class SessionNotFoundException : HmonException
{
    public Guid SessionId { get; }

    public SessionNotFoundException(Guid sessionId)
        : base($"No active and connected session found for ID: {sessionId}")
    {
        SessionId = sessionId;
    }
}
