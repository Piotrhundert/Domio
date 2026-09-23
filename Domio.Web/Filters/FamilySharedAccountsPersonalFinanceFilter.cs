using System.Security.Claims;
using Domio.Application.FamilyFinance;
using Domio.Web.Models.PersonalFinance;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Domio.Web.Filters;

public sealed class FamilySharedAccountsPersonalFinanceFilter(
    IFamilyFinanceService familyFinanceService) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next)
    {
        var controller = context.RouteData.Values["controller"]?.ToString();
        var action = context.RouteData.Values["action"]?.ToString();

        if (!string.Equals(
                controller,
                "PersonalFinance",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                action,
                "Index",
                StringComparison.OrdinalIgnoreCase) ||
            context.Result is not ViewResult viewResult ||
            viewResult.ViewData.Model is not PersonalFinanceIndexViewModel model)
        {
            await next();
            return;
        }

        var userIdValue = context.HttpContext.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            await next();
            return;
        }

        try
        {
            var sharedAccounts =
                await familyFinanceService.GetSharedAccountsForUserAsync(
                    userId,
                    context.HttpContext.RequestAborted);

            var sharedAccountIds = sharedAccounts
                .Select(x => x.AccountId)
                .ToHashSet();

            if (sharedAccountIds.Count > 0)
            {
                var finance = model.Finance;
                var filteredFinance = finance with
                {
                    Accounts = finance.Accounts
                        .Where(x => !sharedAccountIds.Contains(x.AccountId))
                        .ToArray(),
                    RecentTransactions = finance.RecentTransactions
                        .Where(x => !sharedAccountIds.Contains(x.AccountId))
                        .ToArray(),
                    RecurringRules = finance.RecurringRules
                        .Where(x => !sharedAccountIds.Contains(x.AccountId))
                        .ToArray(),
                    RecurringOccurrences = finance.RecurringOccurrences
                        .Where(x => !sharedAccountIds.Contains(x.AccountId))
                        .ToArray()
                };

                viewResult.ViewData.Model =
                    new PersonalFinanceIndexViewModel
                    {
                        Finance = filteredFinance,
                        OwnContributionObligations =
                            model.OwnContributionObligations,
                        OwnContributionPayments =
                            model.OwnContributionPayments,
                        CanPayContributions =
                            model.CanPayContributions
                    };
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Brak dostępu do finansów rodzinnych nie może blokować
            // standardowego widoku finansów osobistych.
        }

        await next();
    }
}
