using System.Security.Claims;
using Domio.Application.Notifications;
using Domio.Web.Models.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Domio.Web.Controllers;

[Authorize]
public sealed class NotificationsController(
    INotificationService notificationService,
    IFamilyAndGoalNotificationScanService familyAndGoalNotificationScanService)
    : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? tab,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId =
                GetCurrentUserId();

            await familyAndGoalNotificationScanService.ScanAsync(
                userId,
                cancellationToken);

            var overview =
                await notificationService.GetOverviewAsync(
                    userId,
                    cancellationToken);

            return View(
                new NotificationCenterViewModel
                {
                    Overview = overview,
                    SelectedTab =
                        string.Equals(
                            tab,
                            "history",
                            StringComparison.OrdinalIgnoreCase)
                            ? "history"
                            : "inbox"
                });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet]
    public async Task<IActionResult> Open(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var linkUrl =
                await notificationService.OpenAsync(
                    id,
                    GetCurrentUserId(),
                    cancellationToken);

            if (!string.IsNullOrWhiteSpace(linkUrl) &&
                Url.IsLocalUrl(linkUrl))
            {
                return LocalRedirect(linkUrl);
            }

            return RedirectToAction(
                nameof(Index));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(
        Guid id,
        string? tab,
        CancellationToken cancellationToken)
    {
        try
        {
            await notificationService.MarkReadAsync(
                id,
                GetCurrentUserId(),
                cancellationToken);

            return RedirectToAction(
                nameof(Index),
                new
                {
                    tab =
                        string.Equals(
                            tab,
                            "history",
                            StringComparison.OrdinalIgnoreCase)
                            ? "history"
                            : "inbox"
                });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(
        CancellationToken cancellationToken)
    {
        try
        {
            await notificationService.MarkAllReadAsync(
                GetCurrentUserId(),
                cancellationToken);

            TempData["NotificationMessage"] =
                "Wszystkie powiadomienia oznaczono jako przeczytane.";

            return RedirectToAction(
                nameof(Index));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
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
            : throw new UnauthorizedAccessException(
                "Brak identyfikatora zalogowanego użytkownika.");
    }
}
