namespace Domio.Application.Notifications;

public interface INotificationSettingsService
{
    Task<NotificationUserSettings> GetUserSettingsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task SaveUserSettingsAsync(
        Guid userId,
        UpdateNotificationUserSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<NotificationEmailConfiguration> GetEmailConfigurationAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task SaveEmailConfigurationAsync(
        Guid actorUserId,
        UpdateNotificationEmailConfigurationRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationEmailDeliverySummary>>
        GetRecentEmailDeliveriesAsync(
            Guid actorUserId,
            int take = 30,
            CancellationToken cancellationToken = default);
}
