namespace Dyalog.Hmon.Client.Lib.Exceptions;

using System;

public class HmonHandshakeFailedException : HmonConnectionException
{
    public HmonHandshakeFailedException(string reason, Exception innerException)
        : base($"HMON handshake failed: {reason}", innerException) { }
}
