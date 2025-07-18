namespace Dyalog.Hmon.Client.Lib.Exceptions;

using System;

public class CommandTimeoutException(string commandName, TimeSpan timeout) : HmonException($"The command '{commandName}' did not receive a response within the configured timeout of {timeout.TotalSeconds} seconds.")
{
  public string CommandName { get; } = commandName;
}
