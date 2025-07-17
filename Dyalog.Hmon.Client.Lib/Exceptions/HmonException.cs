namespace Dyalog.Hmon.Client.Lib.Exceptions;

using System;

public class HmonException : Exception
{
    public HmonException(string message) : base(message) { }
    public HmonException(string message, Exception innerException) : base(message, innerException) { }
}
