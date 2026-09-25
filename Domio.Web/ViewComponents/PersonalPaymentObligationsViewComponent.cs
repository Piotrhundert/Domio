using System.Globalization;
using System.Security.Claims;
using Domio.Application.FamilyFinance;
using Domio.Application.HouseholdFinance;
using Domio.Application.Authorization;
using Domio.Domain.FamilyFinance;
using Domio.Domain.HouseholdFinance;
using Domio.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace Domio.Web.ViewComponents;

public sealed class PersonalPaymentObligationsViewComponent(
    IHouseholdFinanceService householdFinanceService,
    IFamilyFinanceService familyFinanceService,
    IFamilyBudgetService familyBudgetService,
    IFamilyAreaService familyAreaService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = ViewContext.HttpContext.User;
        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var actorUserId))
        {
            return Content(string.Empty);
        }

        var todayUtc = DateTime.UtcNow.Date;
        var year = todayUtc.Year;
        var month = todayUtc.Month;
        var periodKey = todayUtc.ToString(
            "yyyy-MM",
            CultureInfo.InvariantCulture);

        var showAll = string.Equals(
            ViewContext.HttpContext.Request.Query["paymentView"].ToString(),
            "all",
            StringComparison.OrdinalIgnoreCase);

        var items = new List<PersonalPaymentObligationRow>();
        var replaceLegacyContributionSection =
            !user.HasClaim(
                DomioClaimTypes.Permission,
                SystemPermissions.FinanceHouseholdView);

        var canManageOwnFinance =
            user.HasClaim(
                DomioClaimTypes.Permission,
                SystemPermissions.FinancePersonalManageOwn);

        if (user.HasClaim(
                DomioClaimTypes.Permission,
                SystemPermissions.FinanceHouseholdView))
        {
            try
            {
                var household =
                    await householdFinanceService
                        .GetContributionOverviewAsync(
                            actorUserId);

                replaceLegacyContributionSection = true;

                if (household is not null)
                {
                    var pendingByObligation =
                        household.PaymentRequests
                            .Where(x =>
                                x.IsOwn &&
                                x.StatusCode ==
                                    HouseholdContributionPaymentStatuses.Pending)
                            .GroupBy(x =>
                                x.ObligationId)
                            .ToDictionary(
                                x => x.Key,
                                x => x.OrderByDescending(y => y.SubmittedAtUtc)
                                    .First());

                    foreach (var obligation in household.Obligations
                        .Where(x =>
                            x.IsOwn &&
                            x.PeriodKey == periodKey))
                    {
                        pendingByObligation.TryGetValue(
                            obligation.ObligationId,
                            out var pendingPayment);

                        var isPaid =
                            obligation.OutstandingAmount <= 0m;

                        var isPending =
                            pendingPayment is not null;

                        var isOverdue =
                            !isPaid &&
                            obligation.DueDateUtc.Date < todayUtc;

                        var statusName =
                            isPending
                                ? "Wpłata oczekuje na akceptację"
                                : isOverdue
                                    ? "Po terminie"
                                    : obligation.StatusNamePl;

                        items.Add(
                            new PersonalPaymentObligationRow(
                                PersonalPaymentObligationSources.Household,
                                "DOM",
                                "Składka na budżet domu",
                                obligation.TargetHouseholdAccountName,
                                obligation.PeriodKey,
                                obligation.DueDateUtc,
                                obligation.Amount,
                                obligation.PaidAmount,
                                obligation.OutstandingAmount,
                                obligation.CurrencyCode,
                                statusName,
                                isPaid,
                                isPending,
                                isOverdue,
                                canManageOwnFinance &&
                                    !isPaid &&
                                    !isPending &&
                                    obligation.ReminderActive,
                                obligation.ObligationId,
                                null,
                                null));
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                replaceLegacyContributionSection = false;
            }
        }

        if (user.HasClaim(
                DomioClaimTypes.Permission,
                FamilyFinancePermissions.View))
        {
            try
            {
                var familyOverview =
                    await familyFinanceService
                        .GetOverviewAsync(
                            actorUserId,
                            null,
                            year,
                            month);

                foreach (var group in familyOverview.Groups)
                {
                    var budget =
                        await familyBudgetService
                            .GetOverviewAsync(
                                group.FamilyGroupId,
                                actorUserId,
                                year,
                                month);

                    var mappedRuleIds = new HashSet<Guid>();

                    var areas =
                        await familyAreaService
                            .GetOverviewAsync(
                                group.FamilyGroupId,
                                actorUserId,
                                year,
                                month);

                    foreach (var area in areas.Areas)
                    {
                        if (area.SystemCode ==
                            FamilyAreaSystemCodes.HouseholdContributions)
                        {
                            continue;
                        }

                        var details =
                            await familyAreaService
                                .GetDetailsAsync(
                                    area.AreaId,
                                    actorUserId,
                                    year,
                                    month);

                        if (details is null)
                        {
                            continue;
                        }

                        foreach (var item in details.Items)
                        {
                            if (item.BeneficiaryPersonId !=
                                    familyOverview.CurrentPersonId ||
                                !item.OccurrenceId.HasValue ||
                                string.Equals(
                                    item.OccurrenceStatusCode,
                                    FamilyRecurringOccurrenceStatuses.Cancelled,
                                    StringComparison.Ordinal))
                            {
                                continue;
                            }

                            mappedRuleIds.Add(item.RuleId);

                            var isPaid =
                                string.Equals(
                                    item.OccurrenceStatusCode,
                                    FamilyRecurringOccurrenceStatuses.Paid,
                                    StringComparison.Ordinal);

                            var remaining =
                                isPaid
                                    ? 0m
                                    : Math.Max(
                                        0m,
                                        item.PlannedAmount -
                                        item.ActualPaidAmount);

                            var dueDate =
                                item.PlannedDateUtc ??
                                new DateTime(
                                    year,
                                    month,
                                    Math.Min(
                                        item.DueDay,
                                        DateTime.DaysInMonth(year, month)),
                                    0,
                                    0,
                                    0,
                                    DateTimeKind.Utc);

                            var isOverdue =
                                remaining > 0m &&
                                dueDate.Date < todayUtc;

                            items.Add(
                                new PersonalPaymentObligationRow(
                                    PersonalPaymentObligationSources.FamilyArea,
                                    "OBSZAR",
                                    item.Name,
                                    $"{area.Name} · {group.Name}",
                                    periodKey,
                                    dueDate,
                                    item.PlannedAmount,
                                    item.ActualPaidAmount,
                                    remaining,
                                    "PLN",
                                    isPaid
                                        ? "Opłacono"
                                        : isOverdue
                                            ? "Po terminie"
                                            : item.OccurrenceStatusNamePl,
                                    isPaid,
                                    false,
                                    isOverdue,
                                    item.CanPay,
                                    null,
                                    group.FamilyGroupId,
                                    item.OccurrenceId));
                        }
                    }

                    foreach (var rule in budget.FamilyRules)
                    {
                        if (rule.BeneficiaryPersonId !=
                                familyOverview.CurrentPersonId ||
                            mappedRuleIds.Contains(rule.RuleId) ||
                            !rule.OccurrenceId.HasValue ||
                            !rule.PlannedDateUtc.HasValue ||
                            string.Equals(
                                rule.OccurrenceStatusCode,
                                FamilyRecurringOccurrenceStatuses.Cancelled,
                                StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var isPaid =
                            string.Equals(
                                rule.OccurrenceStatusCode,
                                FamilyRecurringOccurrenceStatuses.Paid,
                                StringComparison.Ordinal);

                        var remaining =
                            isPaid
                                ? 0m
                                : rule.PlannedAmount;

                        var isOverdue =
                            remaining > 0m &&
                            rule.PlannedDateUtc.Value.Date < todayUtc;

                        var canPay =
                            remaining > 0m &&
                            string.Equals(
                                rule.OccurrenceStatusCode,
                                FamilyRecurringOccurrenceStatuses.Planned,
                                StringComparison.Ordinal) &&
                            rule.PlannedDateUtc.Value.Date <= todayUtc;

                        items.Add(
                            new PersonalPaymentObligationRow(
                                PersonalPaymentObligationSources.Family,
                                "RODZINA",
                                rule.Name,
                                group.Name,
                                periodKey,
                                rule.PlannedDateUtc.Value,
                                rule.PlannedAmount,
                                isPaid ? rule.PlannedAmount : 0m,
                                remaining,
                                "PLN",
                                isPaid
                                    ? "Opłacono"
                                    : isOverdue
                                        ? "Po terminie"
                                        : rule.OccurrenceStatusNamePl ?? "Do opłacenia",
                                isPaid,
                                false,
                                isOverdue,
                                canPay,
                                null,
                                group.FamilyGroupId,
                                rule.OccurrenceId));
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Brak dostępu do części rodzinnej nie blokuje widoku finansów osobistych.
            }
        }

        var orderedItems =
            items
                .OrderBy(x => x.IsPaid)
                .ThenByDescending(x => x.IsOverdue)
                .ThenBy(x => x.DueDateUtc)
                .ThenBy(x => x.Title)
                .ToArray();

        var visibleItems =
            showAll
                ? orderedItems
                : orderedItems
                    .Where(x => x.RemainingAmount > 0m)
                    .ToArray();

        var model =
            new PersonalPaymentObligationsViewModel(
                year,
                month,
                showAll,
                replaceLegacyContributionSection,
                orderedItems.Count(x => x.RemainingAmount > 0m),
                orderedItems.Count(x => x.IsPaid),
                orderedItems
                    .Where(x => x.RemainingAmount > 0m)
                    .Sum(x => x.RemainingAmount),
                visibleItems);

        return View(model);
    }
}

public static class PersonalPaymentObligationSources
{
    public const string Household = "HouseholdContribution";
    public const string FamilyArea = "FamilyArea";
    public const string Family = "FamilyExpense";
}

public sealed record PersonalPaymentObligationRow(
    string SourceCode,
    string SourceBadge,
    string Title,
    string ContextName,
    string PeriodKey,
    DateTime DueDateUtc,
    decimal Amount,
    decimal PaidAmount,
    decimal RemainingAmount,
    string CurrencyCode,
    string StatusNamePl,
    bool IsPaid,
    bool IsPending,
    bool IsOverdue,
    bool CanPay,
    Guid? HouseholdObligationId,
    Guid? FamilyGroupId,
    Guid? FamilyOccurrenceId);

public sealed record PersonalPaymentObligationsViewModel(
    int Year,
    int Month,
    bool ShowAll,
    bool ReplaceLegacyContributionSection,
    int OpenCount,
    int PaidCount,
    decimal RemainingTotal,
    IReadOnlyList<PersonalPaymentObligationRow> Items);
