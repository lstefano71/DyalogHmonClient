namespace Dyalog.Hmon.Client.Lib;

/// <summary>
/// Represents the types of "facts" that can be requested from a Dyalog interpreter.
/// </summary>
public enum FactType
{
  Host = 1,
  AccountInformation = 2,
  Workspace = 3,
  Threads = 4,
  SuspendedThreads = 5,
  ThreadCount = 6
}

/// <summary>
/// Represents the types of events that a client can subscribe to.
/// </summary>
public enum SubscriptionEvent
{
  WorkspaceCompaction = 1,
  WorkspaceResize = 2,
  UntrappedSignal = 3,
  TrappedSignal = 4,
  ThreadSwitch = 5,
  All = 6
}
