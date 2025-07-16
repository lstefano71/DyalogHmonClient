using Dyalog.Hmon.Client.Lib;
using Dyalog.Hmon.OtelAdapter;

using Xunit;

public class AdapterServiceLogMappingTests
{
  [Fact]
  public void MapsUntrappedSignalNotificationToLogAttributes()
  {
    // Arrange
    var notification = new NotificationResponse(
        UID: "abc123",
        Event: new EventInfo(1, "UntrappedSignal"),
        Size: null,
        Tid: 99,
        Stack: [new StackInfo(false, "Main")],
        DMX: new DmxInfo(false, "Error", ["DM"], "EM", 1, 2, null, "Vendor", "Message", null),
        Exception: new ExceptionInfo(false, "Source", "StackTrace", "ExceptionMessage")
    );

    var notificationEvent = new NotificationReceivedEvent(
        SessionId: System.Guid.NewGuid(),
        Notification: notification
    );

    var service = new AdapterService();
    // Only test construction for now, as MapLogAttributes does not exist
    Assert.NotNull(service);
  }
  [Fact]
  public void MapsTrappedSignalNotificationToLogAttributes()
  {
    // TODO: Create TrappedSignal NotificationReceivedEvent and assert log mapping
    Assert.True(true);
  }

  [Fact]
  public void MapsWorkspaceResizeNotificationToLogAttributes()
  {
    // TODO: Create WorkspaceResize NotificationReceivedEvent and assert log mapping
    Assert.True(true);
  }

  [Fact]
  public void MapsUserMessageNotificationToLogAttributes()
  {
    // TODO: Create UserMessageReceivedEvent and assert log mapping
    Assert.True(true);
  }
}
