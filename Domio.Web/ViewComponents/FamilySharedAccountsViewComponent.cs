using System.Security.Claims;
using Domio.Application.FamilyFinance;
using Microsoft.AspNetCore.Mvc;

namespace Domio.Web.ViewComponents;

public sealed class FamilySharedAccountsViewComponent(
    IFamilyFinanceService familyFinanceService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userIdValue = UserClaimsPrincipal.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Content(string.Empty);
        }

        try
        {
            var accounts =
                await familyFinanceService.GetSharedAccountsForUserAsync(
                    userId,
                    HttpContext.RequestAborted);

            if (accounts.Count == 0)
            {
                return Content(string.Empty);
            }

            return View(accounts);
        }
        catch (UnauthorizedAccessException)
        {
            return Content(string.Empty);
        }
    }
}
