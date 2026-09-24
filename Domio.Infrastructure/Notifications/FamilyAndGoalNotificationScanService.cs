using System.Data;
using System.Data.Common;
using Domio.Application.Notifications;
using Domio.Domain.FamilyFinance;
using Domio.Domain.FinancialGoals;
using Domio.Domain.HouseholdFinance;
using Domio.Domain.Notifications;
using Domio.Domain.PersonalFinance;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.Infrastructure.Notifications;

public sealed class FamilyAndGoalNotificationScanService(
    DomioDbContext dbContext,
    INotificationService notificationService)
    : IFamilyAndGoalNotificationScanService
{
    private static readonly TimeSpan RecentEventWindow =
        TimeSpan.FromDays(45);

    private static readonly TimeSpan RecentContributionWindow =
        TimeSpan.FromDays(90);

    public async Task ScanAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var userContext =
            await GetUserContextAsync(
                userId,
                cancellationToken);

        if (userContext is null)
        {
            return;
        }

        var familyGroups =
            await ReadActiveFamilyGroupsAsync(
                userContext.PersonId,
                cancellationToken);

        foreach (var familyGroup in familyGroups)
        {
            await ScanFamilyMembershipAsync(
                userId,
                familyGroup,
                cancellationToken);

            await ScanChildIncomeAsync(
                userId,
                familyGroup,
                cancellationToken);

            await ScanChildContributionsAsync(
                userId,
                familyGroup,
                cancellationToken);

            await ScanSharedFamilyAccountAsync(
                userId,
                familyGroup,
                cancellationToken);
        }

        await ScanGoalsAsync(
            userId,
            userContext,
            familyGroups,
            cancellationToken);
    }

    private async Task ScanFamilyMembershipAsync(
        Guid userId,
        FamilyGroupRow familyGroup,
        CancellationToken cancellationToken)
    {
        var now =
            DateTime.UtcNow;

        var cutoff =
            now.Subtract(
                RecentEventWindow);

        var members =
            await ReadFamilyMembersAsync(
                familyGroup.FamilyGroupId,
                cancellationToken);

        foreach (var member in members)
        {
            if (member.CreatedAtUtc >= cutoff)
            {
                await notificationService.PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.4.FamilyMemberAdded",
                        NotificationCategoryCodes.Family,
                        NotificationSeverityCodes.Info,
                        "Zmiana w składzie rodziny",
                        $"{member.DisplayName} został(a) dodany(a) do rodziny „{familyGroup.FamilyGroupName}” jako {FamilyRoles.GetNamePl(member.FamilyRoleCode).ToLowerInvariant()}.",
                        true,
                        BuildFamilyLink(
                            familyGroup.FamilyGroupId,
                            "members"),
                        "FamilyMember",
                        member.MembershipId.ToString(),
                        $"M04.10.4:family-member:{member.MembershipId:D}:added",
                        member.CreatedAtUtc),
                    cancellationToken);
            }

            if (member.ValidToUtc.HasValue &&
                member.ValidToUtc.Value >= cutoff)
            {
                await notificationService.PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.4.FamilyMembershipEnded",
                        NotificationCategoryCodes.Family,
                        NotificationSeverityCodes.Important,
                        "Członkostwo w rodzinie zostało zakończone",
                        $"{member.DisplayName} nie jest już aktywnym członkiem rodziny „{familyGroup.FamilyGroupName}”.",
                        true,
                        BuildFamilyLink(
                            familyGroup.FamilyGroupId,
                            "members"),
                        "FamilyMember",
                        member.MembershipId.ToString(),
                        $"M04.10.4:family-member:{member.MembershipId:D}:ended",
                        member.ValidToUtc.Value),
                    cancellationToken);
            }
        }
    }

    private async Task ScanChildIncomeAsync(
        Guid userId,
        FamilyGroupRow familyGroup,
        CancellationToken cancellationToken)
    {
        var now =
            DateTime.UtcNow;

        var eventCutoff =
            now.Subtract(
                RecentEventWindow);

        var receiptCutoff =
            now.Subtract(
                RecentContributionWindow);

        var rules =
            await ReadChildIncomeRulesAsync(
                familyGroup.FamilyGroupId,
                cancellationToken);

        foreach (var rule in rules)
        {
            var amount =
                FamilyFinanceMoney.FromMinorUnits(
                    rule.PlannedAmountMinor);

            if (rule.CreatedAtUtc >= eventCutoff)
            {
                await notificationService.PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.4.ChildIncomeCreated",
                        NotificationCategoryCodes.Family,
                        NotificationSeverityCodes.Info,
                        "Dodano przychód dziecka",
                        $"{rule.ChildDisplayName}: {rule.Name} · plan {amount:N2} zł ({FamilyRecurringFrequencies.GetNamePl(rule.FrequencyCode).ToLowerInvariant()}).",
                        true,
                        BuildFamilyLink(
                            familyGroup.FamilyGroupId,
                            "children"),
                        "FamilyRecurringRule",
                        rule.RuleId.ToString(),
                        $"M04.10.4:child-income:{rule.RuleId:D}:created",
                        rule.CreatedAtUtc),
                    cancellationToken);
            }

            if (!rule.IsActive &&
                rule.UpdatedAtUtc >= eventCutoff)
            {
                await notificationService.PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.4.ChildIncomeDeactivated",
                        NotificationCategoryCodes.Family,
                        NotificationSeverityCodes.Info,
                        "Zakończono planowany przychód dziecka",
                        $"{rule.ChildDisplayName}: plan „{rule.Name}” został zakończony.",
                        false,
                        BuildFamilyLink(
                            familyGroup.FamilyGroupId,
                            "children"),
                        "FamilyRecurringRule",
                        rule.RuleId.ToString(),
                        $"M04.10.4:child-income:{rule.RuleId:D}:deactivated",
                        rule.UpdatedAtUtc),
                    cancellationToken);
            }
        }

        var receipts =
            await ReadChildIncomeReceiptsAsync(
                familyGroup.FamilyGroupId,
                receiptCutoff,
                cancellationToken);

        foreach (var receipt in receipts)
        {
            var amount =
                FamilyFinanceMoney.FromMinorUnits(
                    receipt.AmountMinor);

            await notificationService.PublishAsync(
                new PublishNotificationRequest(
                    [userId],
                    "M04.10.4.ChildIncomeReceived",
                    NotificationCategoryCodes.Family,
                    NotificationSeverityCodes.Important,
                    "Potwierdzono wpływ dla dziecka",
                    $"{receipt.ChildDisplayName}: {receipt.RuleName} · wpłynęło {amount:N2} zł za {receipt.PeriodKey}.",
                    true,
                    BuildFamilyLink(
                        familyGroup.FamilyGroupId,
                        "children"),
                    "FamilyIncomeReceipt",
                    receipt.ReceiptId.ToString(),
                    $"M04.10.4:child-income-receipt:{receipt.ReceiptId:D}",
                    receipt.CreatedAtUtc),
                cancellationToken);
        }
    }

    private async Task ScanChildContributionsAsync(
        Guid userId,
        FamilyGroupRow familyGroup,
        CancellationToken cancellationToken)
    {
        var today =
            DateTime.UtcNow.Date;

        var eventCutoff =
            DateTime.UtcNow.Subtract(
                RecentEventWindow);

        var obligations =
            await ReadChildContributionObligationsAsync(
                familyGroup,
                today.AddMonths(-2),
                today.AddMonths(2),
                cancellationToken);

        foreach (var obligation in obligations)
        {
            var outstandingMinor =
                Math.Max(
                    0,
                    obligation.AmountMinor -
                    obligation.PaidAmountMinor);

            var outstanding =
                HouseholdFinanceMoney.FromMinorUnits(
                    outstandingMinor);

            if (outstandingMinor <= 0 ||
                obligation.StatusCode ==
                    HouseholdContributionStatuses.Paid)
            {
                if (obligation.UpdatedAtUtc >= eventCutoff)
                {
                    await notificationService.PublishAsync(
                        new PublishNotificationRequest(
                            [userId],
                            "M04.10.4.ChildContributionPaid",
                            NotificationCategoryCodes.Family,
                            NotificationSeverityCodes.Important,
                            "Składka dziecka dla domu została opłacona",
                            $"{obligation.ChildDisplayName}: składka za {obligation.PeriodKey} jest rozliczona.",
                            true,
                            BuildFamilyLink(
                                familyGroup.FamilyGroupId,
                                "child-contributions"),
                            "HouseholdContributionObligation",
                            obligation.ObligationId.ToString(),
                            $"M04.10.4:child-contribution:{obligation.ObligationId:D}:paid",
                            obligation.UpdatedAtUtc),
                        cancellationToken);
                }

                continue;
            }

            if (obligation.PaidAmountMinor > 0 &&
                obligation.UpdatedAtUtc >= eventCutoff)
            {
                await notificationService.PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.4.ChildContributionPartiallyPaid",
                        NotificationCategoryCodes.Family,
                        NotificationSeverityCodes.Info,
                        "Składka dziecka jest częściowo opłacona",
                        $"{obligation.ChildDisplayName}: za {obligation.PeriodKey} pozostało {outstanding:N2} zł.",
                        true,
                        BuildFamilyLink(
                            familyGroup.FamilyGroupId,
                            "child-contributions"),
                        "HouseholdContributionObligation",
                        obligation.ObligationId.ToString(),
                        $"M04.10.4:child-contribution:{obligation.ObligationId:D}:partial:{obligation.PaidAmountMinor}",
                        obligation.UpdatedAtUtc),
                    cancellationToken);
            }

            var daysToDue =
                (obligation.DueDateUtc.Date -
                 today)
                .Days;

            if (daysToDue < 0)
            {
                await notificationService.PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.4.ChildContributionOverdue",
                        NotificationCategoryCodes.Family,
                        NotificationSeverityCodes.Warning,
                        "Składka dziecka jest po terminie",
                        $"{obligation.ChildDisplayName}: za {obligation.PeriodKey} pozostało {outstanding:N2} zł. Termin: {obligation.DueDateUtc.ToLocalTime():dd.MM.yyyy}.",
                        true,
                        BuildFamilyLink(
                            familyGroup.FamilyGroupId,
                            "child-contributions"),
                        "HouseholdContributionObligation",
                        obligation.ObligationId.ToString(),
                        $"M04.10.4:child-contribution:{obligation.ObligationId:D}:overdue"),
                    cancellationToken);
            }
            else if (daysToDue == 0)
            {
                await notificationService.PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.4.ChildContributionDueToday",
                        NotificationCategoryCodes.Family,
                        NotificationSeverityCodes.Warning,
                        "Termin składki dziecka jest dzisiaj",
                        $"{obligation.ChildDisplayName}: do wpłaty pozostało {outstanding:N2} zł.",
                        true,
                        BuildFamilyLink(
                            familyGroup.FamilyGroupId,
                            "child-contributions"),
                        "HouseholdContributionObligation",
                        obligation.ObligationId.ToString(),
                        $"M04.10.4:child-contribution:{obligation.ObligationId:D}:due-today"),
                    cancellationToken);
            }
            else if (daysToDue <=
                     Math.Clamp(
                         obligation.ReminderDays,
                         0,
                         31))
            {
                await notificationService.PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.4.ChildContributionUpcoming",
                        NotificationCategoryCodes.Family,
                        NotificationSeverityCodes.Important,
                        "Zbliża się termin składki dziecka",
                        $"{obligation.ChildDisplayName}: {outstanding:N2} zł do {obligation.DueDateUtc.ToLocalTime():dd.MM.yyyy}.",
                        true,
                        BuildFamilyLink(
                            familyGroup.FamilyGroupId,
                            "child-contributions"),
                        "HouseholdContributionObligation",
                        obligation.ObligationId.ToString(),
                        $"M04.10.4:child-contribution:{obligation.ObligationId:D}:upcoming"),
                    cancellationToken);
            }
        }
    }

    private async Task ScanSharedFamilyAccountAsync(
        Guid userId,
        FamilyGroupRow familyGroup,
        CancellationToken cancellationToken)
    {
        var cutoff =
            DateTime.UtcNow.Subtract(
                RecentEventWindow);

        var accounts =
            await ReadSharedAccountsAsync(
                familyGroup.FamilyGroupId,
                cancellationToken);

        foreach (var account in accounts)
        {
            if (account.CreatedAtUtc >= cutoff)
            {
                await notificationService.PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.4.FamilySharedAccountCreated",
                        NotificationCategoryCodes.Family,
                        NotificationSeverityCodes.Important,
                        "Utworzono wspólne konto rodziny",
                        $"Rodzina „{familyGroup.FamilyGroupName}” ma nowe wspólne konto: {account.AccountName}.",
                        true,
                        BuildFamilyLink(
                            familyGroup.FamilyGroupId,
                            "shared"),
                        "FamilySharedAccount",
                        account.SharedAccountId.ToString(),
                        $"M04.10.4:shared-account:{account.SharedAccountId:D}:created",
                        account.CreatedAtUtc),
                    cancellationToken);
            }

            var transactions =
                await ReadSharedAccountTransactionsAsync(
                    account.PersonalAccountId,
                    cutoff,
                    cancellationToken);

            foreach (var transaction in transactions)
            {
                var amount =
                    Math.Abs(
                        PersonalFinanceMoney.FromMinorUnits(
                            transaction.AmountMinor));

                var operationName =
                    transaction.KindCode ==
                        PersonalTransactionKinds.Income
                        ? "przychód"
                        : "wydatek";

                await notificationService.PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.4.FamilySharedAccountOperation",
                        NotificationCategoryCodes.Family,
                        NotificationSeverityCodes.Info,
                        "Operacja na wspólnym koncie",
                        $"{account.AccountName}: {operationName} {amount:N2} zł.",
                        false,
                        BuildFamilyLink(
                            familyGroup.FamilyGroupId,
                            "shared"),
                        "PersonalFinancialTransaction",
                        transaction.TransactionId.ToString(),
                        $"M04.10.4:shared-account-transaction:{transaction.TransactionId:D}",
                        transaction.CreatedAtUtc),
                    cancellationToken);
            }
        }
    }

    private async Task ScanGoalsAsync(
        Guid userId,
        UserContext userContext,
        IReadOnlyList<FamilyGroupRow> familyGroups,
        CancellationToken cancellationToken)
    {
        var goals =
            new List<GoalRow>();

        goals.AddRange(
            await ReadGoalsAsync(
                FinancialGoalScopes.Personal,
                userContext.PersonId,
                cancellationToken));

        if (userContext.HouseholdId.HasValue)
        {
            goals.AddRange(
                await ReadGoalsAsync(
                    FinancialGoalScopes.Household,
                    userContext.HouseholdId.Value,
                    cancellationToken));
        }

        foreach (var familyGroup in familyGroups)
        {
            goals.AddRange(
                await ReadGoalsAsync(
                    FinancialGoalScopes.Family,
                    familyGroup.FamilyGroupId,
                    cancellationToken));
        }

        foreach (var goal in goals
            .GroupBy(x => x.GoalId)
            .Select(x => x.First()))
        {
            await ScanGoalAsync(
                userId,
                goal,
                cancellationToken);
        }
    }

    private async Task ScanGoalAsync(
        Guid userId,
        GoalRow goal,
        CancellationToken cancellationToken)
    {
        var now =
            DateTime.UtcNow;

        var eventCutoff =
            now.Subtract(
                RecentEventWindow);

        var contributionCutoff =
            now.Subtract(
                RecentContributionWindow);

        var isShared =
            goal.ScopeCode !=
                FinancialGoalScopes.Personal;

        var link =
            $"/FinancialGoals/Details/{goal.GoalId:D}";

        if (goal.CreatedAtUtc >= eventCutoff)
        {
            await notificationService.PublishAsync(
                new PublishNotificationRequest(
                    [userId],
                    "M04.10.4.FinancialGoalCreated",
                    NotificationCategoryCodes.Goal,
                    NotificationSeverityCodes.Info,
                    "Utworzono cel finansowy",
                    $"Cel „{goal.Name}”: {FinancialGoalMoney.FromMinorUnits(goal.TargetAmountMinor):N2} zł.",
                    isShared,
                    link,
                    "FinancialGoal",
                    goal.GoalId.ToString(),
                    $"M04.10.4:goal:{goal.GoalId:D}:created",
                    goal.CreatedAtUtc),
                cancellationToken);
        }

        if (!goal.IsActive &&
            goal.ArchivedAtUtc.HasValue &&
            goal.ArchivedAtUtc.Value >= eventCutoff)
        {
            await notificationService.PublishAsync(
                new PublishNotificationRequest(
                    [userId],
                    "M04.10.4.FinancialGoalArchived",
                    NotificationCategoryCodes.Goal,
                    NotificationSeverityCodes.Info,
                    "Cel finansowy został zarchiwizowany",
                    $"Cel „{goal.Name}” przeniesiono do archiwum.",
                    isShared,
                    link,
                    "FinancialGoal",
                    goal.GoalId.ToString(),
                    $"M04.10.4:goal:{goal.GoalId:D}:archived",
                    goal.ArchivedAtUtc.Value),
                cancellationToken);
        }

        var contributions =
            await ReadGoalContributionsAsync(
                goal.GoalId,
                contributionCutoff,
                cancellationToken);

        foreach (var contribution in contributions)
        {
            var amount =
                FinancialGoalMoney.FromMinorUnits(
                    contribution.AmountMinor);

            var contributorIsCurrentUser =
                contribution.CreatedByUserId ==
                userId;

            await notificationService.PublishAsync(
                new PublishNotificationRequest(
                    [userId],
                    "M04.10.4.FinancialGoalContributionAdded",
                    NotificationCategoryCodes.Goal,
                    NotificationSeverityCodes.Info,
                    "Dodano środki do celu",
                    $"Cel „{goal.Name}”: dodano {amount:N2} zł.",
                    isShared &&
                    !contributorIsCurrentUser,
                    link,
                    "FinancialGoalContribution",
                    contribution.ContributionId.ToString(),
                    $"M04.10.4:goal-contribution:{contribution.ContributionId:D}",
                    contribution.CreatedAtUtc),
                cancellationToken);
        }

        var savedMinor =
            await ReadGoalSavedMinorAsync(
                goal.GoalId,
                cancellationToken);

        if (goal.TargetAmountMinor > 0)
        {
            var progress =
                savedMinor * 100m /
                goal.TargetAmountMinor;

            var milestone =
                progress >= 100m
                    ? 100
                    : progress >= 75m
                        ? 75
                        : progress >= 50m
                            ? 50
                            : 0;

            if (milestone > 0)
            {
                await notificationService.PublishAsync(
                    new PublishNotificationRequest(
                        [userId],
                        "M04.10.4.FinancialGoalMilestone",
                        NotificationCategoryCodes.Goal,
                        milestone >= 100
                            ? NotificationSeverityCodes.Important
                            : NotificationSeverityCodes.Info,
                        milestone >= 100
                            ? "Cel finansowy został osiągnięty"
                            : $"Cel finansowy osiągnął {milestone}%",
                        milestone >= 100
                            ? $"Cel „{goal.Name}” został sfinansowany w całości."
                            : $"Cel „{goal.Name}” osiągnął co najmniej {milestone}% planowanej kwoty.",
                        true,
                        link,
                        "FinancialGoal",
                        goal.GoalId.ToString(),
                        $"M04.10.4:goal:{goal.GoalId:D}:milestone:{milestone}"),
                    cancellationToken);
            }
        }

        if (!goal.IsActive ||
            !goal.TargetDateUtc.HasValue ||
            savedMinor >= goal.TargetAmountMinor)
        {
            return;
        }

        var remaining =
            FinancialGoalMoney.FromMinorUnits(
                Math.Max(
                    0,
                    goal.TargetAmountMinor -
                    savedMinor));

        var daysToTarget =
            (goal.TargetDateUtc.Value.Date -
             now.Date)
            .Days;

        if (daysToTarget < 0)
        {
            await notificationService.PublishAsync(
                new PublishNotificationRequest(
                    [userId],
                    "M04.10.4.FinancialGoalOverdue",
                    NotificationCategoryCodes.Goal,
                    NotificationSeverityCodes.Warning,
                    "Termin celu finansowego minął",
                    $"Cel „{goal.Name}”: pozostało {remaining:N2} zł. Termin: {goal.TargetDateUtc.Value.ToLocalTime():dd.MM.yyyy}.",
                    true,
                    link,
                    "FinancialGoal",
                    goal.GoalId.ToString(),
                    $"M04.10.4:goal:{goal.GoalId:D}:overdue"),
                cancellationToken);
        }
        else if (daysToTarget == 0)
        {
            await notificationService.PublishAsync(
                new PublishNotificationRequest(
                    [userId],
                    "M04.10.4.FinancialGoalDueToday",
                    NotificationCategoryCodes.Goal,
                    NotificationSeverityCodes.Warning,
                    "Termin celu finansowego jest dzisiaj",
                    $"Cel „{goal.Name}”: do zebrania pozostało {remaining:N2} zł.",
                    true,
                    link,
                    "FinancialGoal",
                    goal.GoalId.ToString(),
                    $"M04.10.4:goal:{goal.GoalId:D}:due-today"),
                cancellationToken);
        }
        else if (daysToTarget <= 7)
        {
            await notificationService.PublishAsync(
                new PublishNotificationRequest(
                    [userId],
                    "M04.10.4.FinancialGoalUpcoming",
                    NotificationCategoryCodes.Goal,
                    NotificationSeverityCodes.Important,
                    "Zbliża się termin celu finansowego",
                    $"Cel „{goal.Name}”: pozostało {remaining:N2} zł do {goal.TargetDateUtc.Value.ToLocalTime():dd.MM.yyyy}.",
                    true,
                    link,
                    "FinancialGoal",
                    goal.GoalId.ToString(),
                    $"M04.10.4:goal:{goal.GoalId:D}:upcoming"),
                cancellationToken);
        }
    }

    private async Task<UserContext?> GetUserContextAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var person =
            await (
                from account in dbContext.UserAccounts
                    .AsNoTracking()
                join personRow in dbContext.People
                    .AsNoTracking()
                    on account.PersonId equals personRow.Id
                where
                    account.Id == userId &&
                    account.IsActive &&
                    personRow.IsActive
                select new
                {
                    PersonId =
                        personRow.Id
                })
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (person is null)
        {
            return null;
        }

        var householdId =
            await (
                from membership in dbContext.HouseholdMembers
                    .AsNoTracking()
                join household in dbContext.Households
                    .AsNoTracking()
                    on membership.HouseholdId equals household.Id
                where
                    membership.PersonId == person.PersonId &&
                    membership.IsActive &&
                    household.IsActive
                orderby membership.JoinedAtUtc descending
                select (Guid?)household.Id)
                .FirstOrDefaultAsync(
                    cancellationToken);

        return new UserContext(
            person.PersonId,
            householdId);
    }

    private async Task<IReadOnlyList<FamilyGroupRow>>
        ReadActiveFamilyGroupsAsync(
            Guid personId,
            CancellationToken cancellationToken)
    {
        var result =
            new List<FamilyGroupRow>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT g.Id, g.HouseholdId, g.Name
                    FROM FamilyGroups g
                    INNER JOIN FamilyMembers m
                        ON m.FamilyGroupId = g.Id
                    WHERE m.PersonId = $personId
                      AND m.ValidToUtc IS NULL
                      AND g.IsActive = 1
                    ORDER BY g.Name;
                    """;

                AddParameter(
                    command,
                    "$personId",
                    personId);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(
                    cancellationToken))
                {
                    result.Add(
                        new FamilyGroupRow(
                            reader.GetGuid(0),
                            reader.GetGuid(1),
                            reader.GetString(2)));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<IReadOnlyList<FamilyMemberEventRow>>
        ReadFamilyMembersAsync(
            Guid familyGroupId,
            CancellationToken cancellationToken)
    {
        var result =
            new List<FamilyMemberEventRow>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT m.Id, m.PersonId, m.FamilyRoleCode,
                           m.ValidToUtc, m.CreatedAtUtc,
                           p.DisplayName, p.FirstName, p.LastName
                    FROM FamilyMembers m
                    INNER JOIN People p
                        ON p.Id = m.PersonId
                    WHERE m.FamilyGroupId = $familyGroupId
                    ORDER BY m.CreatedAtUtc DESC;
                    """;

                AddParameter(
                    command,
                    "$familyGroupId",
                    familyGroupId);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(
                    cancellationToken))
                {
                    result.Add(
                        new FamilyMemberEventRow(
                            reader.GetGuid(0),
                            reader.GetGuid(1),
                            reader.GetString(2),
                            reader.IsDBNull(3)
                                ? null
                                : ReadDateTime(
                                    reader,
                                    3),
                            ReadDateTime(
                                reader,
                                4),
                            BuildDisplayName(
                                reader.IsDBNull(5)
                                    ? null
                                    : reader.GetString(5),
                                reader.GetString(6),
                                reader.GetString(7))));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<IReadOnlyList<ChildIncomeRuleRow>>
        ReadChildIncomeRulesAsync(
            Guid familyGroupId,
            CancellationToken cancellationToken)
    {
        var result =
            new List<ChildIncomeRuleRow>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT r.Id, r.Name, r.PlannedAmountMinor,
                           r.FrequencyCode, r.IsActive,
                           r.CreatedAtUtc, r.UpdatedAtUtc,
                           p.DisplayName, p.FirstName, p.LastName
                    FROM FamilyRecurringRules r
                    INNER JOIN People p
                        ON p.Id = r.BeneficiaryPersonId
                    WHERE r.FamilyGroupId = $familyGroupId
                      AND r.RuleTypeCode = 'Income'
                    ORDER BY r.CreatedAtUtc DESC
                    LIMIT 150;
                    """;

                AddParameter(
                    command,
                    "$familyGroupId",
                    familyGroupId);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(
                    cancellationToken))
                {
                    result.Add(
                        new ChildIncomeRuleRow(
                            reader.GetGuid(0),
                            reader.GetString(1),
                            reader.GetInt64(2),
                            reader.GetString(3),
                            ReadBoolean(
                                reader,
                                4),
                            ReadDateTime(
                                reader,
                                5),
                            ReadDateTime(
                                reader,
                                6),
                            BuildDisplayName(
                                reader.IsDBNull(7)
                                    ? null
                                    : reader.GetString(7),
                                reader.GetString(8),
                                reader.GetString(9))));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<IReadOnlyList<ChildIncomeReceiptRow>>
        ReadChildIncomeReceiptsAsync(
            Guid familyGroupId,
            DateTime createdFromUtc,
            CancellationToken cancellationToken)
    {
        var result =
            new List<ChildIncomeReceiptRow>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT receipt.Id, receipt.PeriodKey,
                           receipt.AmountMinor, receipt.CreatedAtUtc,
                           rule.Name,
                           p.DisplayName, p.FirstName, p.LastName
                    FROM FamilyIncomeReceipts receipt
                    INNER JOIN FamilyRecurringRules rule
                        ON rule.Id = receipt.RuleId
                    INNER JOIN People p
                        ON p.Id = receipt.BeneficiaryPersonId
                    WHERE receipt.FamilyGroupId = $familyGroupId
                      AND receipt.CreatedAtUtc >= $createdFromUtc
                    ORDER BY receipt.CreatedAtUtc DESC;
                    """;

                AddParameter(
                    command,
                    "$familyGroupId",
                    familyGroupId);
                AddParameter(
                    command,
                    "$createdFromUtc",
                    createdFromUtc);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(
                    cancellationToken))
                {
                    result.Add(
                        new ChildIncomeReceiptRow(
                            reader.GetGuid(0),
                            reader.GetString(1),
                            reader.GetInt64(2),
                            ReadDateTime(
                                reader,
                                3),
                            reader.GetString(4),
                            BuildDisplayName(
                                reader.IsDBNull(5)
                                    ? null
                                    : reader.GetString(5),
                                reader.GetString(6),
                                reader.GetString(7))));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<IReadOnlyList<ChildContributionRow>>
        ReadChildContributionObligationsAsync(
            FamilyGroupRow familyGroup,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken)
    {
        var result =
            new List<ChildContributionRow>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT obligation.Id, obligation.PeriodKey,
                           obligation.AmountMinor, obligation.PaidAmountMinor,
                           obligation.DueDateUtc, obligation.StatusCode,
                           obligation.UpdatedAtUtc, rule.ReminderDays,
                           p.DisplayName, p.FirstName, p.LastName
                    FROM HouseholdContributionObligations obligation
                    INNER JOIN HouseholdContributionRules rule
                        ON rule.Id = obligation.ContributionRuleId
                    INNER JOIN HouseholdMembers hm
                        ON hm.Id = obligation.HouseholdMemberId
                    INNER JOIN FamilyMembers fm
                        ON fm.PersonId = hm.PersonId
                    INNER JOIN People p
                        ON p.Id = hm.PersonId
                    WHERE obligation.HouseholdId = $householdId
                      AND hm.HouseholdId = $householdId
                      AND hm.IsActive = 1
                      AND fm.FamilyGroupId = $familyGroupId
                      AND fm.FamilyRoleCode = 'Child'
                      AND fm.ValidToUtc IS NULL
                      AND rule.IncomeRuleId IS NULL
                      AND obligation.DueDateUtc >= $fromUtc
                      AND obligation.DueDateUtc < $toUtc
                      AND obligation.StatusCode NOT IN ('Cancelled', 'Corrected')
                    ORDER BY obligation.DueDateUtc;
                    """;

                AddParameter(
                    command,
                    "$householdId",
                    familyGroup.HouseholdId);
                AddParameter(
                    command,
                    "$familyGroupId",
                    familyGroup.FamilyGroupId);
                AddParameter(
                    command,
                    "$fromUtc",
                    fromUtc);
                AddParameter(
                    command,
                    "$toUtc",
                    toUtc);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(
                    cancellationToken))
                {
                    result.Add(
                        new ChildContributionRow(
                            reader.GetGuid(0),
                            reader.GetString(1),
                            reader.GetInt64(2),
                            reader.GetInt64(3),
                            ReadDateTime(
                                reader,
                                4),
                            reader.GetString(5),
                            ReadDateTime(
                                reader,
                                6),
                            reader.GetInt32(7),
                            BuildDisplayName(
                                reader.IsDBNull(8)
                                    ? null
                                    : reader.GetString(8),
                                reader.GetString(9),
                                reader.GetString(10))));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<IReadOnlyList<SharedAccountRow>>
        ReadSharedAccountsAsync(
            Guid familyGroupId,
            CancellationToken cancellationToken)
    {
        var result =
            new List<SharedAccountRow>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT shared.Id, shared.PersonalAccountId,
                           shared.CreatedAtUtc, account.Name
                    FROM FamilySharedAccounts shared
                    INNER JOIN PersonalFinancialAccounts account
                        ON account.Id = shared.PersonalAccountId
                    WHERE shared.FamilyGroupId = $familyGroupId
                      AND shared.ClosedAtUtc IS NULL
                    ORDER BY shared.CreatedAtUtc DESC;
                    """;

                AddParameter(
                    command,
                    "$familyGroupId",
                    familyGroupId);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(
                    cancellationToken))
                {
                    result.Add(
                        new SharedAccountRow(
                            reader.GetGuid(0),
                            reader.GetGuid(1),
                            ReadDateTime(
                                reader,
                                2),
                            reader.GetString(3)));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<IReadOnlyList<SharedAccountTransactionRow>>
        ReadSharedAccountTransactionsAsync(
            Guid personalAccountId,
            DateTime createdFromUtc,
            CancellationToken cancellationToken)
    {
        var result =
            new List<SharedAccountTransactionRow>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT Id, KindCode, AmountMinor, CreatedAtUtc
                    FROM PersonalFinancialTransactions
                    WHERE AccountId = $accountId
                      AND CreatedAtUtc >= $createdFromUtc
                      AND KindCode <> 'OpeningBalance'
                    ORDER BY CreatedAtUtc DESC
                    LIMIT 100;
                    """;

                AddParameter(
                    command,
                    "$accountId",
                    personalAccountId);
                AddParameter(
                    command,
                    "$createdFromUtc",
                    createdFromUtc);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(
                    cancellationToken))
                {
                    result.Add(
                        new SharedAccountTransactionRow(
                            reader.GetGuid(0),
                            reader.GetString(1),
                            reader.GetInt64(2),
                            ReadDateTime(
                                reader,
                                3)));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<IReadOnlyList<GoalRow>>
        ReadGoalsAsync(
            string scopeCode,
            Guid ownerId,
            CancellationToken cancellationToken)
    {
        var result =
            new List<GoalRow>();

        var ownerColumn =
            scopeCode switch
            {
                FinancialGoalScopes.Personal =>
                    "OwnerPersonId",
                FinancialGoalScopes.Household =>
                    "HouseholdId",
                FinancialGoalScopes.Family =>
                    "FamilyGroupId",
                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(scopeCode))
            };

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    $"""
                    SELECT Id, ScopeCode, Name, TargetAmountMinor,
                           TargetDateUtc, IsActive, CreatedByUserId,
                           CreatedAtUtc, UpdatedAtUtc, ArchivedAtUtc
                    FROM FinancialGoals
                    WHERE ScopeCode = $scopeCode
                      AND {ownerColumn} = $ownerId
                    ORDER BY CreatedAtUtc DESC
                    LIMIT 250;
                    """;

                AddParameter(
                    command,
                    "$scopeCode",
                    scopeCode);
                AddParameter(
                    command,
                    "$ownerId",
                    ownerId);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(
                    cancellationToken))
                {
                    result.Add(
                        new GoalRow(
                            reader.GetGuid(0),
                            reader.GetString(1),
                            reader.GetString(2),
                            reader.GetInt64(3),
                            reader.IsDBNull(4)
                                ? null
                                : ReadDateTime(
                                    reader,
                                    4),
                            ReadBoolean(
                                reader,
                                5),
                            reader.GetGuid(6),
                            ReadDateTime(
                                reader,
                                7),
                            ReadDateTime(
                                reader,
                                8),
                            reader.IsDBNull(9)
                                ? null
                                : ReadDateTime(
                                    reader,
                                    9)));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<IReadOnlyList<GoalContributionRow>>
        ReadGoalContributionsAsync(
            Guid goalId,
            DateTime createdFromUtc,
            CancellationToken cancellationToken)
    {
        var result =
            new List<GoalContributionRow>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT Id, AmountMinor, CreatedByUserId, CreatedAtUtc
                    FROM FinancialGoalContributions
                    WHERE GoalId = $goalId
                      AND CreatedAtUtc >= $createdFromUtc
                    ORDER BY CreatedAtUtc DESC;
                    """;

                AddParameter(
                    command,
                    "$goalId",
                    goalId);
                AddParameter(
                    command,
                    "$createdFromUtc",
                    createdFromUtc);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(
                    cancellationToken))
                {
                    result.Add(
                        new GoalContributionRow(
                            reader.GetGuid(0),
                            reader.GetInt64(1),
                            reader.GetGuid(2),
                            ReadDateTime(
                                reader,
                                3)));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<long> ReadGoalSavedMinorAsync(
        Guid goalId,
        CancellationToken cancellationToken)
    {
        long result = 0;

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT COALESCE(SUM(AmountMinor), 0)
                    FROM FinancialGoalContributions
                    WHERE GoalId = $goalId;
                    """;

                AddParameter(
                    command,
                    "$goalId",
                    goalId);

                result =
                    Convert.ToInt64(
                        await command.ExecuteScalarAsync(
                            cancellationToken)
                        ?? 0);
            },
            cancellationToken);

        return result;
    }

    private async Task WithConnectionAsync(
        Func<DbConnection, Task> action,
        CancellationToken cancellationToken)
    {
        var connection =
            dbContext.Database.GetDbConnection();

        var shouldClose =
            connection.State !=
            ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(
                cancellationToken);
        }

        try
        {
            await action(
                connection);
        }
        finally
        {
            if (shouldClose &&
                dbContext.Database.CurrentTransaction is null)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        object? value)
    {
        var parameter =
            command.CreateParameter();

        parameter.ParameterName =
            name;

        parameter.Value =
            value ??
            DBNull.Value;

        command.Parameters.Add(
            parameter);
    }

    private static DateTime ReadDateTime(
        DbDataReader reader,
        int ordinal)
    {
        var value =
            reader.GetValue(
                ordinal);

        if (value is DateTime dateTime)
        {
            return dateTime.Kind ==
                   DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(
                    dateTime,
                    DateTimeKind.Utc)
                : dateTime.ToUniversalTime();
        }

        var parsed =
            Convert.ToDateTime(
                value,
                System.Globalization.CultureInfo.InvariantCulture);

        return DateTime.SpecifyKind(
            parsed,
            DateTimeKind.Utc);
    }

    private static bool ReadBoolean(
        DbDataReader reader,
        int ordinal)
    {
        var value =
            reader.GetValue(
                ordinal);

        return value switch
        {
            bool boolean =>
                boolean,
            long integer =>
                integer != 0,
            int integer =>
                integer != 0,
            _ =>
                Convert.ToBoolean(
                    value,
                    System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    private static string BuildDisplayName(
        string? displayName,
        string firstName,
        string lastName) =>
        string.IsNullOrWhiteSpace(
            displayName)
            ? $"{firstName} {lastName}".Trim()
            : displayName.Trim();

    private static string BuildFamilyLink(
        Guid familyGroupId,
        string tab) =>
        $"/FamilyFinance?familyGroupId={familyGroupId:D}&tab={Uri.EscapeDataString(tab)}";

    private sealed record UserContext(
        Guid PersonId,
        Guid? HouseholdId);

    private sealed record FamilyGroupRow(
        Guid FamilyGroupId,
        Guid HouseholdId,
        string FamilyGroupName);

    private sealed record FamilyMemberEventRow(
        Guid MembershipId,
        Guid PersonId,
        string FamilyRoleCode,
        DateTime? ValidToUtc,
        DateTime CreatedAtUtc,
        string DisplayName);

    private sealed record ChildIncomeRuleRow(
        Guid RuleId,
        string Name,
        long PlannedAmountMinor,
        string FrequencyCode,
        bool IsActive,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc,
        string ChildDisplayName);

    private sealed record ChildIncomeReceiptRow(
        Guid ReceiptId,
        string PeriodKey,
        long AmountMinor,
        DateTime CreatedAtUtc,
        string RuleName,
        string ChildDisplayName);

    private sealed record ChildContributionRow(
        Guid ObligationId,
        string PeriodKey,
        long AmountMinor,
        long PaidAmountMinor,
        DateTime DueDateUtc,
        string StatusCode,
        DateTime UpdatedAtUtc,
        int ReminderDays,
        string ChildDisplayName);

    private sealed record SharedAccountRow(
        Guid SharedAccountId,
        Guid PersonalAccountId,
        DateTime CreatedAtUtc,
        string AccountName);

    private sealed record SharedAccountTransactionRow(
        Guid TransactionId,
        string KindCode,
        long AmountMinor,
        DateTime CreatedAtUtc);

    private sealed record GoalRow(
        Guid GoalId,
        string ScopeCode,
        string Name,
        long TargetAmountMinor,
        DateTime? TargetDateUtc,
        bool IsActive,
        Guid CreatedByUserId,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc,
        DateTime? ArchivedAtUtc);

    private sealed record GoalContributionRow(
        Guid ContributionId,
        long AmountMinor,
        Guid CreatedByUserId,
        DateTime CreatedAtUtc);
}
