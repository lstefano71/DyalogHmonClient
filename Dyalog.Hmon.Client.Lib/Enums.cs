namespace Dyalog.Hmon.Client.Lib;

/// <summary>
/// Represents the types of "facts" that can be requested from a Dyalog interpreter.
/// </summary>
public enum FactType
{
    Host,
    AccountInformation,
    Workspace,
    Threads,
    SuspendedThreads,
    ThreadCount
}

/// <summary>
/// Represents the types of events that a client can subscribe to.
/// </summary>
public enum SubscriptionEvent
{
    WorkspaceCompaction,
    WorkspaceResize,
    UntrappedSignal,
    TrappedSignal,
    ThreadSwitch,
    All
}
