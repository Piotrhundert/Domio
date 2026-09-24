namespace Domio.Application.Notifications;

public interface IFamilyAndGoalNotificationScanService
{
    Task ScanAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
