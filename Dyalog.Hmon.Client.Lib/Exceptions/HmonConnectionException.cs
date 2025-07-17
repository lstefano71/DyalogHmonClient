namespace Dyalog.Hmon.Client.Lib.Exceptions;

using System;

public class HmonConnectionException : HmonException
{
    public HmonConnectionException(string message, Exception innerException)
        : base(message, innerException) { }
}
