using Xunit;
using Dyalog.Hmon.OtelAdapter;
using Dyalog.Hmon.Client.Lib;
using System.Collections.Generic;

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
            Stack: new List<StackInfo> { new StackInfo(false, "Main") },
            DMX: new DmxInfo(false, "Error", new[] { "DM" }, "EM", 1, 2, null, "Vendor", "Message", null),
            Exception: new ExceptionInfo(false, "Source", "StackTrace", "ExceptionMessage")
        );

        var notificationEvent = new NotificationReceivedEvent(
            SessionId: System.Guid.NewGuid(),
            Notification: notification
        );

        var service = new AdapterService();

        // Act
        var logAttributes = service.MapLogAttributes(notificationEvent);

        // Assert
        Assert.Equal("UntrappedSignal", logAttributes["event"]);
        Assert.Equal("abc123", logAttributes["uid"]);
        Assert.Equal(99, logAttributes["tid"]);
        Assert.NotNull(logAttributes["thread.DMX"]);
        Assert.NotNull(logAttributes["thread.Stack"]);
        Assert.NotNull(logAttributes["thread.Info"]);
    }
}
