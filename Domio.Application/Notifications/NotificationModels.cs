namespace Domio.Application.Notifications;

public sealed record NotificationItem(
    Guid NotificationId,
    string EventCode,
    string CategoryCode,
    string CategoryNamePl,
    string SeverityCode,
    string SeverityNamePl,
    string Title,
    string Message,
    string? LinkUrl,
    string? SourceType,
    string? SourceId,
    bool ShowInInbox,
    bool IsRead,
    DateTime? ReadAtUtc,
    DateTime CreatedAtUtc);

public sealed record NotificationHeaderSummary(
    int UnreadCount,
    IReadOnlyList<NotificationItem> RecentNotifications);

public sealed record NotificationCenterOverview(
    int UnreadCount,
    int ImportantUnreadCount,
    int WarningUnreadCount,
    IReadOnlyList<NotificationItem> Inbox,
    IReadOnlyList<NotificationItem> ActivityHistory);

public sealed record PublishNotificationRequest(
    IReadOnlyCollection<Guid> RecipientUserIds,
    string EventCode,
    string CategoryCode,
    string SeverityCode,
    string Title,
    string Message,
    bool ShowInInbox = true,
    string? LinkUrl = null,
    string? SourceType = null,
    string? SourceId = null,
    string? DedupeKey = null,
    DateTime? CreatedAtUtc = null);
