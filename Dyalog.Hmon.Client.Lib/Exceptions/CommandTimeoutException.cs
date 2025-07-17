namespace Dyalog.Hmon.Client.Lib.Exceptions;

using System;

public class CommandTimeoutException : HmonException
{
    public string CommandName { get; }

    public CommandTimeoutException(string commandName, TimeSpan timeout)
        : base($"The command '{commandName}' did not receive a response within the configured timeout of {timeout.TotalSeconds} seconds.")
    {
        CommandName = commandName;
    }
}
