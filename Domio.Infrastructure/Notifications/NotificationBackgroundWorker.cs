using Domio.Application.Notifications;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Domio.Infrastructure.Notifications;

public sealed class NotificationBackgroundWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationBackgroundWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await DelaySafeAsync(
            TimeSpan.FromSeconds(5),
            stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var pollIntervalMinutes =
                1;

            try
            {
                pollIntervalMinutes =
                    await ProcessCycleAsync(
                        stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Cykl powiadomień Domio zakończył się błędem.");
            }

            await DelaySafeAsync(
                TimeSpan.FromMinutes(
                    Math.Clamp(
                        pollIntervalMinutes,
                        1,
                        60)),
                stoppingToken);
        }
    }

    private async Task<int> ProcessCycleAsync(
        CancellationToken cancellationToken)
    {
        using var scope =
            scopeFactory.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<DomioDbContext>();

        var notificationService =
            scope.ServiceProvider
                .GetRequiredService<INotificationService>();

        var familyScanner =
            scope.ServiceProvider
                .GetRequiredService<IFamilyAndGoalNotificationScanService>();

        var orchestrator =
            scope.ServiceProvider
                .GetRequiredService<INotificationDeliveryOrchestratorService>();

        var emailDispatcher =
            scope.ServiceProvider
                .GetRequiredService<INotificationEmailDispatcher>();

        var settingsService =
            scope.ServiceProvider
                .GetRequiredService<NotificationSettingsService>();

        var userIds =
            await dbContext.UserAccounts
                .AsNoTracking()
                .Where(x =>
                    x.IsActive)
                .Select(x =>
                    x.Id)
                .ToArrayAsync(
                    cancellationToken);

        foreach (var userId in userIds)
        {
            try
            {
                await familyScanner.ScanAsync(
                    userId,
                    cancellationToken);

                // GetHeader uruchamia skan zdarzeń domu, składek i faktur.
                await notificationService.GetHeaderAsync(
                    userId,
                    1,
                    cancellationToken);

                await orchestrator.ProcessUserAsync(
                    userId,
                    cancellationToken);
            }
            catch (UnauthorizedAccessException)
            {
                // Użytkownik bez Notifications.View nie uczestniczy w kanale.
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Nie udało się przetworzyć powiadomień użytkownika {UserId}.",
                    userId);
            }
        }

        await emailDispatcher.DispatchPendingAsync(
            100,
            cancellationToken);

        var configuration =
            await settingsService
                .GetEmailConfigurationForDeliveryAsync(
                    cancellationToken);

        return Math.Clamp(
            configuration.PollIntervalMinutes,
            1,
            60);
    }

    private static async Task DelaySafeAsync(
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(
                delay,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Zamykanie aplikacji.
        }
    }
}
