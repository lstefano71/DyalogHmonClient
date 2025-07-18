namespace Dyalog.Hmon.Client.Lib.Exceptions;

using System;

public class HmonConnectionException(string message, Exception innerException) : HmonException(message, innerException)
{
}
