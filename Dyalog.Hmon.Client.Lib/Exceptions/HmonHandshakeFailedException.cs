namespace Dyalog.Hmon.Client.Lib.Exceptions;

using System;

public class HmonHandshakeFailedException(string reason, Exception innerException) : HmonConnectionException($"HMON handshake failed: {reason}", innerException)
{
}
