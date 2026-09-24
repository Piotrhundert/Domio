namespace Domio.Application.Notifications;

public interface INotificationService
{
    Task<NotificationHeaderSummary> GetHeaderAsync(
        Guid userId,
        int take = 5,
        CancellationToken cancellationToken = default);

    Task<NotificationCenterOverview> GetOverviewAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<int> PublishAsync(
        PublishNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task MarkReadAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task MarkAllReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<string?> OpenAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
