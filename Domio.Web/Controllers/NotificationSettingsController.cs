using System.Security.Claims;
using Domio.Application.Authorization;
using Domio.Application.Notifications;
using Domio.Domain.Users;
using Domio.Infrastructure.Persistence;
using Domio.Web.Models.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Domio.Web.Controllers;

[Authorize(Policy = SystemPermissions.NotificationsView)]
public sealed class NotificationSettingsController(
    INotificationSettingsService settingsService,
    INotificationEmailDispatcher emailDispatcher,
    DomioDbContext dbContext) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var userId =
            GetCurrentUserId();

        var settings =
            await settingsService.GetUserSettingsAsync(
                userId,
                cancellationToken);

        var accountEmail =
            await dbContext.UserAccounts
                .AsNoTracking()
                .Where(x =>
                    x.Id == userId)
                .Select(x =>
                    x.Email)
                .SingleOrDefaultAsync(
                    cancellationToken);

        var canManageEmail =
            User.HasClaim(
                DomioClaimTypes.Permission,
                SystemPermissions.NotificationsManage);

        NotificationEmailConfigurationFormViewModel?
            emailConfiguration =
                null;

        IReadOnlyList<NotificationEmailDeliverySummary>
            emailHistory =
                [];

        IReadOnlyList<NotificationMessageTemplateFormViewModel>
            messageTemplates =
                [];

        if (canManageEmail)
        {
            var configuration =
                await settingsService.GetEmailConfigurationAsync(
                    userId,
                    cancellationToken);

            emailConfiguration =
                BuildEmailModel(
                    configuration,
                    accountEmail);

            emailHistory =
                await settingsService
                    .GetRecentEmailDeliveriesAsync(
                        userId,
                        30,
                        cancellationToken);

            messageTemplates =
                (await settingsService
                    .GetMessageTemplatesAsync(
                        userId,
                        cancellationToken))
                .Select(BuildTemplateModel)
                .ToArray();
        }

        return View(
            new NotificationSettingsPageViewModel
            {
                UserSettings =
                    BuildUserModel(
                        settings),
                EmailConfiguration =
                    emailConfiguration,
                EmailHistory =
                    emailHistory,
                MessageTemplates =
                    messageTemplates,
                CanManageEmail =
                    canManageEmail,
                AccountEmail =
                    accountEmail
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveUser(
        NotificationUserSettingsFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["NotificationSettingsError"] =
                "Sprawdź poprawność ustawień użytkownika.";

            return RedirectToAction(
                nameof(Index));
        }

        try
        {
            await settingsService.SaveUserSettingsAsync(
                GetCurrentUserId(),
                new UpdateNotificationUserSettingsRequest(
                    model.InAppEnabled,
                    model.EmailEnabled,
                    model.EmailAddress,
                    model.Reminder7Days,
                    model.Reminder3Days,
                    model.Reminder1Day,
                    model.ReminderDueDay,
                    model.ReminderOverdue,
                    model.NotifySystem,
                    model.NotifyPersonalFinance,
                    model.NotifyHousehold,
                    model.NotifyContributions,
                    model.NotifyInvoices,
                    model.NotifyFamily,
                    model.NotifyGoals,
                    model.NotifyUsers),
                cancellationToken);

            TempData["NotificationSettingsMessage"] =
                "Ustawienia powiadomień zostały zapisane.";
        }
        catch (ArgumentException exception)
        {
            TempData["NotificationSettingsError"] =
                exception.Message;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToAction(
            nameof(Index));
    }

    [Authorize(Policy = SystemPermissions.NotificationsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEmail(
        NotificationEmailConfigurationFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["NotificationSettingsError"] =
                "Sprawdź poprawność konfiguracji SMTP.";

            return RedirectToAction(
                nameof(Index));
        }

        try
        {
            await settingsService.SaveEmailConfigurationAsync(
                GetCurrentUserId(),
                new UpdateNotificationEmailConfigurationRequest(
                    model.IsEnabled,
                    model.SenderName,
                    model.SenderEmail,
                    model.SmtpHost,
                    model.SmtpPort,
                    model.SmtpUsername,
                    model.SmtpPassword,
                    model.UseSsl,
                    model.ApplicationBaseUrl,
                    model.PollIntervalMinutes),
                cancellationToken);

            TempData["NotificationSettingsMessage"] =
                "Konfiguracja poczty wychodzącej została zapisana.";
        }
        catch (ArgumentException exception)
        {
            TempData["NotificationSettingsError"] =
                exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            TempData["NotificationSettingsError"] =
                exception.Message;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToAction(
            nameof(Index));
    }

    [Authorize(Policy = SystemPermissions.NotificationsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTemplate(
        NotificationMessageTemplateFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["NotificationSettingsError"] =
                "Sprawdź temat i treść szablonu powiadomienia.";

            return RedirectToAction(
                nameof(Index));
        }

        try
        {
            await settingsService.SaveMessageTemplateAsync(
                GetCurrentUserId(),
                new UpdateNotificationMessageTemplateRequest(
                    model.CategoryCode,
                    model.SubjectTemplate,
                    model.BodyTemplate),
                cancellationToken);

            TempData["NotificationSettingsMessage"] =
                $"Szablon kategorii „{model.CategoryNamePl}” został zapisany.";
        }
        catch (ArgumentException exception)
        {
            TempData["NotificationSettingsError"] =
                exception.Message;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToAction(
            nameof(Index));
    }

    [Authorize(Policy = SystemPermissions.NotificationsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetTemplate(
        string categoryCode,
        CancellationToken cancellationToken)
    {
        try
        {
            await settingsService.ResetMessageTemplateAsync(
                GetCurrentUserId(),
                categoryCode,
                cancellationToken);

            TempData["NotificationSettingsMessage"] =
                "Przywrócono domyślną treść dla wybranej kategorii.";
        }
        catch (ArgumentException exception)
        {
            TempData["NotificationSettingsError"] =
                exception.Message;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToAction(
            nameof(Index));
    }

    [Authorize(Policy = SystemPermissions.NotificationsManage)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTest(
        string? testRecipientEmail,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                testRecipientEmail))
        {
            TempData["NotificationSettingsError"] =
                "Podaj adres odbiorcy wiadomości testowej.";

            return RedirectToAction(
                nameof(Index));
        }

        try
        {
            await emailDispatcher.SendTestAsync(
                GetCurrentUserId(),
                testRecipientEmail,
                cancellationToken);

            TempData["NotificationSettingsMessage"] =
                "Wiadomość testowa została wysłana.";
        }
        catch (ArgumentException exception)
        {
            TempData["NotificationSettingsError"] =
                exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            TempData["NotificationSettingsError"] =
                exception.Message;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToAction(
            nameof(Index));
    }

    private Guid GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        return Guid.TryParse(
            value,
            out var userId)
            ? userId
            : throw new InvalidOperationException(
                "Nie można ustalić zalogowanego użytkownika.");
    }

    private static NotificationUserSettingsFormViewModel
        BuildUserModel(
            NotificationUserSettings settings) =>
        new()
        {
            InAppEnabled =
                settings.InAppEnabled,
            EmailEnabled =
                settings.EmailEnabled,
            EmailAddress =
                settings.EmailAddress,
            Reminder7Days =
                settings.Reminder7Days,
            Reminder3Days =
                settings.Reminder3Days,
            Reminder1Day =
                settings.Reminder1Day,
            ReminderDueDay =
                settings.ReminderDueDay,
            ReminderOverdue =
                settings.ReminderOverdue,
            NotifySystem =
                settings.NotifySystem,
            NotifyPersonalFinance =
                settings.NotifyPersonalFinance,
            NotifyHousehold =
                settings.NotifyHousehold,
            NotifyContributions =
                settings.NotifyContributions,
            NotifyInvoices =
                settings.NotifyInvoices,
            NotifyFamily =
                settings.NotifyFamily,
            NotifyGoals =
                settings.NotifyGoals,
            NotifyUsers =
                settings.NotifyUsers
        };

    private static NotificationMessageTemplateFormViewModel
        BuildTemplateModel(
            NotificationMessageTemplate template) =>
        new()
        {
            CategoryCode =
                template.CategoryCode,
            CategoryNamePl =
                template.CategoryNamePl,
            SubjectTemplate =
                template.SubjectTemplate,
            BodyTemplate =
                template.BodyTemplate,
            IsCustomized =
                template.IsCustomized,
            UpdatedAtUtc =
                template.UpdatedAtUtc
        };

    private static NotificationEmailConfigurationFormViewModel
        BuildEmailModel(
            NotificationEmailConfiguration configuration,
            string? accountEmail) =>
        new()
        {
            IsConfigured =
                configuration.IsConfigured,
            IsEnabled =
                configuration.IsEnabled,
            SenderName =
                configuration.SenderName,
            SenderEmail =
                configuration.SenderEmail,
            SmtpHost =
                configuration.SmtpHost,
            SmtpPort =
                configuration.SmtpPort,
            SmtpUsername =
                configuration.SmtpUsername,
            SmtpPassword =
                null,
            HasPassword =
                configuration.HasPassword,
            UseSsl =
                configuration.UseSsl,
            ApplicationBaseUrl =
                configuration.ApplicationBaseUrl,
            PollIntervalMinutes =
                configuration.PollIntervalMinutes,
            UpdatedAtUtc =
                configuration.UpdatedAtUtc,
            TestRecipientEmail =
                accountEmail
        };
}
