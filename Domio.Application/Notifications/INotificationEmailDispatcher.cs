namespace Domio.Application.Notifications;

public interface INotificationEmailDispatcher
{
    Task SendTestAsync(
        Guid actorUserId,
        string recipientEmail,
        CancellationToken cancellationToken = default);

    Task<int> DispatchPendingAsync(
        int maxBatch = 50,
        CancellationToken cancellationToken = default);
}
