namespace Domio.Application.Notifications;

public interface INotificationDeliveryOrchestratorService
{
    Task ProcessUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
