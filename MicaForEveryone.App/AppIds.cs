using System;

namespace MicaForEveryone.App;

internal static class AppIds
{
    public const string InstanceKey = "MicaForEveryoneFork";
    public const string StartupTaskName = "MicaForEveryoneFork";
    public const string NotificationIconWindowClass = "MicaForEveryoneForkNotificationIcon";
    public const string NotificationIconTooltip = "Mica For Everyone Fork";
    public const nuint NotificationIconId = 1;
    public const nuint NotificationIconRestoreTimerId = 1;

    public static readonly Guid NotificationIconGuid = new("6ce0713b-e19b-4e77-9f72-6ad980dfed7d");
}
