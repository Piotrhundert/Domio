using System.Security.Claims;
using Domio.Application.Authorization;
using Domio.Application.Notifications;
using Domio.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace Domio.Web.ViewComponents;

public sealed class NotificationBellViewComponent(
    INotificationService notificationService,
    IFamilyAndGoalNotificationScanService familyAndGoalNotificationScanService,
    INotificationDeliveryOrchestratorService notificationDeliveryOrchestratorService)
    : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user =
            ViewContext.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true ||
            !user.HasClaim(
                DomioClaimTypes.Permission,
                SystemPermissions.NotificationsView))
        {
            return Content(
                string.Empty);
        }

        var value =
            user.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(
                value,
                out var userId))
        {
            return Content(
                string.Empty);
        }

        try
        {
            await familyAndGoalNotificationScanService.ScanAsync(
                userId,
                ViewContext.HttpContext.RequestAborted);

            // Pierwszy odczyt uruchamia skan zdarzeń domu, składek i faktur.
            await notificationService.GetHeaderAsync(
                userId,
                1,
                ViewContext.HttpContext.RequestAborted);

            await notificationDeliveryOrchestratorService.ProcessUserAsync(
                userId,
                ViewContext.HttpContext.RequestAborted);

            var model =
                await notificationService.GetHeaderAsync(
                    userId,
                    5,
                    ViewContext.HttpContext.RequestAborted);

            return View(model);
        }
        catch (UnauthorizedAccessException)
        {
            return Content(
                string.Empty);
        }
    }
}
