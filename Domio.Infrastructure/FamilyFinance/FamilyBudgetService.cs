using System.Data;
using System.Data.Common;
using Domio.Application.Auditing;
using Domio.Application.FamilyFinance;
using Domio.Domain.FamilyFinance;
using Domio.Domain.HouseholdFinance;
using Domio.Domain.PersonalFinance;
using Domio.Infrastructure.Authorization;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Domio.Infrastructure.FamilyFinance;

public sealed class FamilyBudgetService(
    DomioDbContext dbContext,
    IAuditService auditService) : IFamilyBudgetService
{
    public async Task<FamilyBudgetOverview> GetOverviewAsync(
        Guid familyGroupId,
        Guid actorUserId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            FamilyFinancePermissions.View,
            cancellationToken);

        ValidatePeriod(year, month);

        var actor = await GetActorAsync(actorUserId, cancellationToken);
        var familyGroup = await EnsureMembershipAsync(
            familyGroupId,
            actor.PersonId,
            actor.HouseholdId,
            cancellationToken);

        await EnsureFamilyOccurrencesAsync(
            familyGroupId,
            year,
            month,
            cancellationToken);

        var members = await GetMembersAsync(familyGroupId, cancellationToken);
        var memberByPersonId = members.ToDictionary(x => x.PersonId);
        var periodKey = BuildPeriodKey(year, month);
        var monthStart = MonthStart(year, month);
        var monthEnd = monthStart.AddMonths(1);

        var plannedItems = new List<BudgetAmountItem>();
        var actualItems = new List<BudgetAmountItem>();

        var familyOccurrences = await GetFamilyOccurrencesAsync(
            familyGroupId,
            periodKey,
            cancellationToken);

        foreach (var occurrence in familyOccurrences)
        {
            plannedItems.Add(
                new BudgetAmountItem(
                    occurrence.CategoryCode,
                    occurrence.PlannedAmountMinor,
                    null,
                    occurrence.BeneficiaryPersonId));
        }

        var links = await GetActiveLinksAsync(familyGroupId, cancellationToken);

        var sharedRecurringLinks = links
            .Where(x =>
                x.SourceType == FamilyBudgetSourceTypes.PersonalRecurringRule &&
                x.PersonId.HasValue &&
                memberByPersonId.TryGetValue(x.PersonId.Value, out var member) &&
                member.ShareRecurringRules)
            .ToArray();

        if (sharedRecurringLinks.Length > 0)
        {
            var ruleIds = sharedRecurringLinks
                .Select(x => x.SourceId)
                .Distinct()
                .ToArray();

            var occurrences = await dbContext.PersonalRecurringOccurrences
                .AsNoTracking()
                .Where(x =>
                    ruleIds.Contains(x.RecurringRuleId) &&
                    x.PlannedDateUtc >= monthStart &&
                    x.PlannedDateUtc < monthEnd &&
                    x.KindCode == PersonalTransactionKinds.Expense &&
                    x.StatusCode != PersonalRecurringOccurrenceStatuses.Cancelled)
                .ToArrayAsync(cancellationToken);

            var linkByRule = sharedRecurringLinks
                .GroupBy(x => x.SourceId)
                .ToDictionary(x => x.Key, x => x.First());

            foreach (var occurrence in occurrences)
            {
                if (!linkByRule.TryGetValue(
                        occurrence.RecurringRuleId,
                        out var link))
                {
                    continue;
                }

                plannedItems.Add(
                    new BudgetAmountItem(
                        link.CategoryCode,
                        occurrence.PlannedAmountMinor,
                        link.PersonId,
                        link.BeneficiaryPersonId));
            }
        }

        // Aktywne, jawnie utworzone FamilyBudgetLink są częścią historii rodziny.
        // Wyłączenie udostępniania blokuje tworzenie nowych prywatnych powiązań,
        // ale nie usuwa wcześniej świadomie udostępnionych wartości z historii.
        var actualLinks = links
            .Where(x =>
                x.PeriodKey == periodKey &&
                x.SourceType != FamilyBudgetSourceTypes.PersonalRecurringRule)
            .ToArray();

        var actualSummaries = await BuildActualLinkSummariesAsync(
            actualLinks,
            revealPersonalDetails: false,
            cancellationToken);

        foreach (var summary in actualSummaries)
        {
            if (!summary.Amount.HasValue)
            {
                continue;
            }

            actualItems.Add(
                new BudgetAmountItem(
                    summary.CategoryCode,
                    FamilyFinanceMoney.ToMinorUnitsAllowNegative(summary.Amount.Value),
                    summary.PersonId,
                    summary.BeneficiaryPersonId));
        }

        var categorySummaries = FamilyBudgetCategories.All
            .Select(category =>
                new FamilyBudgetCategorySummary(
                    category.Code,
                    category.NamePl,
                    SumMajor(plannedItems.Where(x => x.CategoryCode == category.Code)),
                    SumMajor(actualItems.Where(x => x.CategoryCode == category.Code))))
            .Where(x => x.PlannedAmount != 0m || x.ActualAmount != 0m)
            .ToArray();

        var personExpenseSummaries = members
            .Where(x => x.FamilyRoleCode == FamilyRoles.Adult)
            .Select(member =>
                new FamilyPersonExpenseSummary(
                    member.PersonId,
                    SumMajor(plannedItems.Where(x => x.PersonId == member.PersonId)),
                    SumMajor(actualItems.Where(x => x.PersonId == member.PersonId))))
            .ToArray();

        var childCosts = members
            .Where(x => x.FamilyRoleCode == FamilyRoles.Child)
            .Select(child =>
                new FamilyChildCostSummary(
                    child.PersonId,
                    child.DisplayName,
                    SumMajor(plannedItems.Where(x => x.BeneficiaryPersonId == child.PersonId)),
                    SumMajor(actualItems.Where(x => x.BeneficiaryPersonId == child.PersonId))))
            .ToArray();

        var rules = await GetRuleSummariesAsync(
            familyGroupId,
            members,
            cancellationToken);

        var publicActualLinks = actualSummaries
            .Where(x => x.SourceType != FamilyBudgetSourceTypes.PersonalTransaction)
            .ToArray();

        return new FamilyBudgetOverview(
            familyGroup.FamilyGroupId,
            year,
            month,
            SumMajor(plannedItems),
            SumMajor(actualItems),
            categorySummaries,
            personExpenseSummaries,
            childCosts,
            rules,
            publicActualLinks);
    }

    public async Task<Guid> CreateRecurringExpenseAsync(
        CreateFamilyRecurringExpenseRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            FamilyFinancePermissions.Manage,
            cancellationToken);

        var actor = await GetActorAsync(actorUserId, cancellationToken);
        await EnsureMembershipAsync(
            request.FamilyGroupId,
            actor.PersonId,
            actor.HouseholdId,
            cancellationToken);

        var name = NormalizeRequiredText(request.Name, "Nazwa kosztu", 160);

        if (!FamilyBudgetCategories.IsValid(request.CategoryCode))
        {
            throw new ArgumentException("Wybierz poprawną kategorię rodzinną.");
        }

        if (!FamilyRecurringFrequencies.IsValid(request.FrequencyCode))
        {
            throw new ArgumentException("Wybierz poprawną częstotliwość.");
        }

        if (request.DueDay < 1 || request.DueDay > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.DueDay),
                "Dzień terminu musi mieścić się w zakresie 1-31.");
        }

        var amountMinor = FamilyFinanceMoney.ToMinorUnits(request.PlannedAmount);
        var activeFrom = NormalizeUtcDate(request.ActiveFromUtc);
        DateTime? activeTo = request.ActiveToUtc.HasValue
            ? NormalizeUtcDate(request.ActiveToUtc.Value)
            : null;

        if (activeTo.HasValue && activeTo.Value < activeFrom)
        {
            throw new ArgumentException(
                "Data końcowa nie może być wcześniejsza od daty początku.");
        }

        if (request.FrequencyCode == FamilyRecurringFrequencies.Once)
        {
            activeTo = activeFrom;
        }

        if (request.BeneficiaryPersonId.HasValue)
        {
            await EnsureBeneficiaryAsync(
                request.FamilyGroupId,
                request.BeneficiaryPersonId.Value,
                cancellationToken);
        }

        var now = DateTime.UtcNow;
        var ruleId = Guid.NewGuid();

        await ExecuteAsync(
            """
            INSERT INTO FamilyRecurringRules
                (Id, FamilyGroupId, Name, RuleTypeCode, CategoryCode,
                 PlannedAmountMinor, FrequencyCode, DueDay, BeneficiaryPersonId,
                 ActiveFromUtc, ActiveToUtc, IsActive, CreatedByUserId,
                 CreatedAtUtc, UpdatedAtUtc)
            VALUES
                ($id, $groupId, $name, $typeCode, $categoryCode,
                 $amountMinor, $frequencyCode, $dueDay, $beneficiaryPersonId,
                 $activeFromUtc, $activeToUtc, 1, $actorUserId,
                 $now, $now);
            """,
            [
                P("$id", ruleId),
                P("$groupId", request.FamilyGroupId),
                P("$name", name),
                P("$typeCode", FamilyRecurringRuleTypes.Expense),
                P("$categoryCode", request.CategoryCode),
                P("$amountMinor", amountMinor),
                P("$frequencyCode", request.FrequencyCode),
                P("$dueDay", request.DueDay),
                P("$beneficiaryPersonId", request.BeneficiaryPersonId),
                P("$activeFromUtc", activeFrom),
                P("$activeToUtc", activeTo),
                P("$actorUserId", actorUserId),
                P("$now", now)
            ],
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.8.2.FamilyRecurringExpenseCreated",
                EntityType: "FamilyRecurringRule",
                EntityId: ruleId.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Dodano rodzinny plan wydatku. Operacja planowana nie zmienia żadnego salda."),
            cancellationToken);

        return ruleId;
    }

    public async Task DeactivateRecurringExpenseAsync(
        Guid ruleId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            FamilyFinancePermissions.Manage,
            cancellationToken);

        var actor = await GetActorAsync(actorUserId, cancellationToken);
        var rule = await GetRuleByIdAsync(ruleId, cancellationToken)
            ?? throw new InvalidOperationException("Nie znaleziono reguły rodzinnej.");

        await EnsureMembershipAsync(
            rule.FamilyGroupId,
            actor.PersonId,
            actor.HouseholdId,
            cancellationToken);

        var now = DateTime.UtcNow;
        var changed = await ExecuteAsync(
            """
            UPDATE FamilyRecurringRules
            SET IsActive = 0,
                ActiveToUtc = CASE
                    WHEN ActiveToUtc IS NULL OR ActiveToUtc > $now THEN $now
                    ELSE ActiveToUtc
                END,
                UpdatedAtUtc = $now
            WHERE Id = $id
              AND IsActive = 1;
            """,
            [P("$now", now), P("$id", ruleId)],
            cancellationToken);

        if (changed == 0)
        {
            throw new InvalidOperationException("Reguła jest już nieaktywna.");
        }

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.8.2.FamilyRecurringExpenseDeactivated",
                EntityType: "FamilyRecurringRule",
                EntityId: ruleId.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Dezaktywowano rodzinną regułę kosztu. Historyczne wystąpienia pozostają zachowane."),
            cancellationToken);
    }

    public async Task<FamilyBudgetLinkForm> GetLinkFormAsync(
        Guid familyGroupId,
        Guid actorUserId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            FamilyFinancePermissions.View,
            cancellationToken);

        ValidatePeriod(year, month);

        var actor = await GetActorAsync(actorUserId, cancellationToken);
        var familyGroup = await EnsureMembershipAsync(
            familyGroupId,
            actor.PersonId,
            actor.HouseholdId,
            cancellationToken);

        var members = await GetMembersAsync(familyGroupId, cancellationToken);
        var ownMember = members.Single(x => x.PersonId == actor.PersonId);
        var canManage = await PermissionEnforcement.HasUserAsync(
            dbContext,
            actorUserId,
            FamilyFinancePermissions.Manage,
            cancellationToken);

        var monthStart = MonthStart(year, month);
        var monthEnd = monthStart.AddMonths(1);
        var periodKey = BuildPeriodKey(year, month);
        var links = await GetActiveLinksAsync(familyGroupId, cancellationToken);
        var activeKeys = links
            .Select(x => $"{x.SourceType}|{x.SourceId:D}")
            .ToHashSet(StringComparer.Ordinal);

        var candidates = new List<FamilyBudgetCandidate>();

        if (ownMember.ShareFamilyExpenses)
        {
            var privateExpenses = await (
                from transaction in dbContext.PersonalFinancialTransactions.AsNoTracking()
                join account in dbContext.PersonalFinancialAccounts.AsNoTracking()
                    on transaction.AccountId equals account.Id
                where
                    transaction.OwnerPersonId == actor.PersonId &&
                    transaction.KindCode == PersonalTransactionKinds.Expense &&
                    transaction.OccurredAtUtc >= monthStart &&
                    transaction.OccurredAtUtc < monthEnd &&
                    account.CurrencyCode == "PLN"
                orderby transaction.OccurredAtUtc descending
                select new
                {
                    transaction.Id,
                    transaction.OccurredAtUtc,
                    transaction.AmountMinor,
                    transaction.CategoryCode,
                    transaction.Counterparty,
                    transaction.Description
                })
                .ToArrayAsync(cancellationToken);

            foreach (var item in privateExpenses)
            {
                var key = $"{FamilyBudgetSourceTypes.PersonalTransaction}|{item.Id:D}";
                if (activeKeys.Contains(key))
                {
                    continue;
                }

                candidates.Add(
                    new FamilyBudgetCandidate(
                        FamilyBudgetSourceTypes.PersonalTransaction,
                        item.Id,
                        FamilyBudgetSourceTypes.GetNamePl(
                            FamilyBudgetSourceTypes.PersonalTransaction),
                        BuildPrivateTransactionLabel(
                            item.CategoryCode,
                            item.Counterparty,
                            item.Description),
                        item.OccurredAtUtc,
                        FamilyFinanceMoney.FromMinorUnits(-item.AmountMinor),
                        "PLN",
                        false));
            }
        }

        if (ownMember.ShareRecurringRules)
        {
            var recurringRules = await (
                from rule in dbContext.PersonalRecurringRules.AsNoTracking()
                join account in dbContext.PersonalFinancialAccounts.AsNoTracking()
                    on rule.AccountId equals account.Id
                where
                    rule.OwnerPersonId == actor.PersonId &&
                    rule.KindCode == PersonalTransactionKinds.Expense &&
                    rule.IsActive &&
                    account.CurrencyCode == "PLN"
                orderby rule.Name
                select new
                {
                    rule.Id,
                    rule.Name,
                    rule.PlannedAmountMinor,
                    rule.StartDateUtc,
                    rule.CategoryCode
                })
                .ToArrayAsync(cancellationToken);

            foreach (var item in recurringRules)
            {
                var key = $"{FamilyBudgetSourceTypes.PersonalRecurringRule}|{item.Id:D}";
                if (activeKeys.Contains(key))
                {
                    continue;
                }

                candidates.Add(
                    new FamilyBudgetCandidate(
                        FamilyBudgetSourceTypes.PersonalRecurringRule,
                        item.Id,
                        FamilyBudgetSourceTypes.GetNamePl(
                            FamilyBudgetSourceTypes.PersonalRecurringRule),
                        $"{item.Name} · {PersonalFinanceCategories.GetNamePl(item.CategoryCode)}",
                        item.StartDateUtc,
                        FamilyFinanceMoney.FromMinorUnits(item.PlannedAmountMinor),
                        "PLN",
                        true));
            }
        }

        if (canManage)
        {
            var householdExpenses = await (
                from entry in dbContext.HouseholdEntries.AsNoTracking()
                join account in dbContext.HouseholdAccounts.AsNoTracking()
                    on entry.AccountId equals account.Id
                where
                    entry.HouseholdId == familyGroup.HouseholdId &&
                    entry.EntryTypeCode == HouseholdEntryTypes.Expense &&
                    entry.OccurredAtUtc >= monthStart &&
                    entry.OccurredAtUtc < monthEnd &&
                    account.CurrencyCode == "PLN"
                orderby entry.OccurredAtUtc descending
                select new
                {
                    entry.Id,
                    entry.OccurredAtUtc,
                    entry.AmountMinor,
                    entry.CategoryCode,
                    entry.Description
                })
                .ToArrayAsync(cancellationToken);

            foreach (var item in householdExpenses)
            {
                var key = $"{FamilyBudgetSourceTypes.HouseholdEntry}|{item.Id:D}";
                if (activeKeys.Contains(key))
                {
                    continue;
                }

                candidates.Add(
                    new FamilyBudgetCandidate(
                        FamilyBudgetSourceTypes.HouseholdEntry,
                        item.Id,
                        FamilyBudgetSourceTypes.GetNamePl(
                            FamilyBudgetSourceTypes.HouseholdEntry),
                        BuildHouseholdEntryLabel(
                            item.CategoryCode,
                            item.Description),
                        item.OccurredAtUtc,
                        FamilyFinanceMoney.FromMinorUnits(-item.AmountMinor),
                        "PLN",
                        false));
            }
        }

        var visibleLinks = links
            .Where(x =>
                (x.SourceType == FamilyBudgetSourceTypes.PersonalTransaction ||
                 x.SourceType == FamilyBudgetSourceTypes.PersonalRecurringRule)
                    ? x.PersonId == actor.PersonId
                    : canManage)
            .Where(x =>
                x.SourceType == FamilyBudgetSourceTypes.PersonalRecurringRule ||
                x.PeriodKey == periodKey)
            .ToArray();

        var existingLinks = await BuildLinkManagementSummariesAsync(
            visibleLinks,
            actor.PersonId,
            cancellationToken);

        return new FamilyBudgetLinkForm(
            familyGroupId,
            familyGroup.Name,
            year,
            month,
            candidates,
            members
                .Select(x =>
                    new FamilyBudgetBeneficiary(
                        x.PersonId,
                        x.DisplayName,
                        x.FamilyRoleCode))
                .ToArray(),
            existingLinks,
            ownMember.ShareFamilyExpenses,
            ownMember.ShareRecurringRules,
            canManage);
    }

    public async Task<Guid> LinkSourceAsync(
        CreateFamilyBudgetLinkRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            FamilyFinancePermissions.View,
            cancellationToken);

        if (!FamilyBudgetSourceTypes.IsValid(request.SourceType))
        {
            throw new ArgumentException("Nieobsługiwany typ źródła budżetu rodzinnego.");
        }

        if (!FamilyBudgetCategories.IsValid(request.CategoryCode))
        {
            throw new ArgumentException("Wybierz poprawną kategorię rodzinną.");
        }

        var actor = await GetActorAsync(actorUserId, cancellationToken);
        var familyGroup = await EnsureMembershipAsync(
            request.FamilyGroupId,
            actor.PersonId,
            actor.HouseholdId,
            cancellationToken);
        var members = await GetMembersAsync(request.FamilyGroupId, cancellationToken);
        var ownMember = members.Single(x => x.PersonId == actor.PersonId);

        if (request.BeneficiaryPersonId.HasValue)
        {
            await EnsureBeneficiaryAsync(
                request.FamilyGroupId,
                request.BeneficiaryPersonId.Value,
                cancellationToken);
        }

        Guid? sourcePersonId = null;
        string? periodKey = null;

        switch (request.SourceType)
        {
            case FamilyBudgetSourceTypes.PersonalTransaction:
            {
                if (!ownMember.ShareFamilyExpenses)
                {
                    throw new InvalidOperationException(
                        "Najpierw włącz udostępnianie wydatków rodzinnych w ustawieniach własnego udostępniania.");
                }

                var source = await (
                    from transaction in dbContext.PersonalFinancialTransactions.AsNoTracking()
                    join account in dbContext.PersonalFinancialAccounts.AsNoTracking()
                        on transaction.AccountId equals account.Id
                    where
                        transaction.Id == request.SourceId &&
                        transaction.OwnerPersonId == actor.PersonId &&
                        transaction.KindCode == PersonalTransactionKinds.Expense &&
                        account.CurrencyCode == "PLN"
                    select new
                    {
                        transaction.OccurredAtUtc
                    })
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException(
                        "Nie znaleziono wskazanego własnego wydatku albo źródło nie może być udostępnione.");

                sourcePersonId = actor.PersonId;
                periodKey = BuildPeriodKey(source.OccurredAtUtc.Year, source.OccurredAtUtc.Month);
                break;
            }

            case FamilyBudgetSourceTypes.PersonalRecurringRule:
            {
                if (!ownMember.ShareRecurringRules)
                {
                    throw new InvalidOperationException(
                        "Najpierw włącz udostępnianie reguł cyklicznych w ustawieniach własnego udostępniania.");
                }

                var exists = await (
                    from rule in dbContext.PersonalRecurringRules.AsNoTracking()
                    join account in dbContext.PersonalFinancialAccounts.AsNoTracking()
                        on rule.AccountId equals account.Id
                    where
                        rule.Id == request.SourceId &&
                        rule.OwnerPersonId == actor.PersonId &&
                        rule.KindCode == PersonalTransactionKinds.Expense &&
                        rule.IsActive &&
                        account.CurrencyCode == "PLN"
                    select rule.Id)
                    .AnyAsync(cancellationToken);

                if (!exists)
                {
                    throw new InvalidOperationException(
                        "Nie znaleziono wskazanej własnej reguły cyklicznej albo źródło nie może być udostępnione.");
                }

                sourcePersonId = actor.PersonId;
                break;
            }

            case FamilyBudgetSourceTypes.HouseholdEntry:
            {
                var canManage = await PermissionEnforcement.HasUserAsync(
                    dbContext,
                    actorUserId,
                    FamilyFinancePermissions.Manage,
                    cancellationToken);

                if (!canManage)
                {
                    throw new UnauthorizedAccessException(
                        "Powiązanie wydatku domu wymaga uprawnienia zarządzania finansami rodzinnymi.");
                }

                var source = await (
                    from entry in dbContext.HouseholdEntries.AsNoTracking()
                    join account in dbContext.HouseholdAccounts.AsNoTracking()
                        on entry.AccountId equals account.Id
                    where
                        entry.Id == request.SourceId &&
                        entry.HouseholdId == familyGroup.HouseholdId &&
                        entry.EntryTypeCode == HouseholdEntryTypes.Expense &&
                        account.CurrencyCode == "PLN"
                    select new
                    {
                        entry.OccurredAtUtc
                    })
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException(
                        "Nie znaleziono wskazanego wydatku domu albo źródło nie może być powiązane.");

                periodKey = BuildPeriodKey(source.OccurredAtUtc.Year, source.OccurredAtUtc.Month);
                break;
            }
        }

        var now = DateTime.UtcNow;
        var existing = await GetLinkBySourceAsync(
            request.FamilyGroupId,
            request.SourceType,
            request.SourceId,
            cancellationToken);

        if (existing is not null && existing.UnlinkedAtUtc is null)
        {
            throw new InvalidOperationException(
                "To źródło jest już zaliczone do budżetu tej rodziny. Jedna operacja może zostać policzona tylko raz.");
        }

        Guid linkId;

        if (existing is null)
        {
            linkId = Guid.NewGuid();

            await ExecuteAsync(
                """
                INSERT INTO FamilyBudgetLinks
                    (Id, FamilyGroupId, PeriodKey, SourceType, SourceId, PersonId,
                     CategoryCode, BeneficiaryPersonId, LinkedByUserId,
                     LinkedAtUtc, UnlinkedAtUtc)
                VALUES
                    ($id, $groupId, $periodKey, $sourceType, $sourceId, $personId,
                     $categoryCode, $beneficiaryPersonId, $actorUserId,
                     $now, NULL);
                """,
                [
                    P("$id", linkId),
                    P("$groupId", request.FamilyGroupId),
                    P("$periodKey", periodKey),
                    P("$sourceType", request.SourceType),
                    P("$sourceId", request.SourceId),
                    P("$personId", sourcePersonId),
                    P("$categoryCode", request.CategoryCode),
                    P("$beneficiaryPersonId", request.BeneficiaryPersonId),
                    P("$actorUserId", actorUserId),
                    P("$now", now)
                ],
                cancellationToken);
        }
        else
        {
            linkId = existing.Id;

            await ExecuteAsync(
                """
                UPDATE FamilyBudgetLinks
                SET PeriodKey = $periodKey,
                    PersonId = $personId,
                    CategoryCode = $categoryCode,
                    BeneficiaryPersonId = $beneficiaryPersonId,
                    LinkedByUserId = $actorUserId,
                    LinkedAtUtc = $now,
                    UnlinkedAtUtc = NULL
                WHERE Id = $id;
                """,
                [
                    P("$periodKey", periodKey),
                    P("$personId", sourcePersonId),
                    P("$categoryCode", request.CategoryCode),
                    P("$beneficiaryPersonId", request.BeneficiaryPersonId),
                    P("$actorUserId", actorUserId),
                    P("$now", now),
                    P("$id", linkId)
                ],
                cancellationToken);
        }

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.8.2.FamilyBudgetSourceLinked",
                EntityType: "FamilyBudgetLink",
                EntityId: linkId.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    $"Powiązano źródło typu {request.SourceType} z budżetem rodzinnym. Nie utworzono drugiego księgowania."),
            cancellationToken);

        return linkId;
    }

    public async Task UnlinkSourceAsync(
        Guid linkId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            FamilyFinancePermissions.View,
            cancellationToken);

        var actor = await GetActorAsync(actorUserId, cancellationToken);
        var link = await GetLinkByIdAsync(linkId, cancellationToken)
            ?? throw new InvalidOperationException("Nie znaleziono aktywnego powiązania.");

        await EnsureMembershipAsync(
            link.FamilyGroupId,
            actor.PersonId,
            actor.HouseholdId,
            cancellationToken);

        if (link.PersonId != actor.PersonId)
        {
            var canManage = await PermissionEnforcement.HasUserAsync(
                dbContext,
                actorUserId,
                FamilyFinancePermissions.Manage,
                cancellationToken);

            if (!canManage || link.PersonId.HasValue)
            {
                throw new UnauthorizedAccessException(
                    "Możesz odłączyć własne prywatne źródło albo źródło domu, jeśli masz uprawnienie zarządzania.");
            }
        }

        var changed = await ExecuteAsync(
            """
            UPDATE FamilyBudgetLinks
            SET UnlinkedAtUtc = $now
            WHERE Id = $id
              AND UnlinkedAtUtc IS NULL;
            """,
            [P("$now", DateTime.UtcNow), P("$id", linkId)],
            cancellationToken);

        if (changed == 0)
        {
            throw new InvalidOperationException("Powiązanie zostało już odłączone.");
        }

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.8.2.FamilyBudgetSourceUnlinked",
                EntityType: "FamilyBudgetLink",
                EntityId: linkId.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Odłączono źródło od bieżącego budżetu rodzinnego. Źródłowa operacja finansowa i audyt pozostały bez zmian."),
            cancellationToken);
    }

    private async Task EnsureFamilyOccurrencesAsync(
        Guid familyGroupId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var rules = await GetRuleRowsAsync(familyGroupId, cancellationToken);
        var monthStart = MonthStart(year, month);
        var periodKey = BuildPeriodKey(year, month);
        var now = DateTime.UtcNow;

        foreach (var rule in rules.Where(x => x.IsActive || IsMonthWithinRule(x, monthStart)))
        {
            if (!ShouldGenerate(rule, monthStart))
            {
                continue;
            }

            var plannedDay = Math.Min(
                rule.DueDay,
                DateTime.DaysInMonth(year, month));
            var plannedDate = new DateTime(
                year,
                month,
                plannedDay,
                0,
                0,
                0,
                DateTimeKind.Utc);

            await ExecuteAsync(
                """
                INSERT OR IGNORE INTO FamilyRecurringOccurrences
                    (Id, FamilyGroupId, RuleId, PeriodKey, PlannedDateUtc,
                     PlannedAmountMinor, StatusCode, BeneficiaryPersonId,
                     CreatedAtUtc, UpdatedAtUtc)
                VALUES
                    ($id, $groupId, $ruleId, $periodKey, $plannedDateUtc,
                     $amountMinor, $statusCode, $beneficiaryPersonId,
                     $now, $now);
                """,
                [
                    P("$id", Guid.NewGuid()),
                    P("$groupId", familyGroupId),
                    P("$ruleId", rule.Id),
                    P("$periodKey", periodKey),
                    P("$plannedDateUtc", plannedDate),
                    P("$amountMinor", rule.PlannedAmountMinor),
                    P("$statusCode", FamilyRecurringOccurrenceStatuses.Planned),
                    P("$beneficiaryPersonId", rule.BeneficiaryPersonId),
                    P("$now", now)
                ],
                cancellationToken);
        }
    }

    private static bool ShouldGenerate(
        FamilyRuleRow rule,
        DateTime monthStart)
    {
        var startMonth = MonthStart(rule.ActiveFromUtc.Year, rule.ActiveFromUtc.Month);
        if (monthStart < startMonth)
        {
            return false;
        }

        if (rule.ActiveToUtc.HasValue)
        {
            var endMonth = MonthStart(rule.ActiveToUtc.Value.Year, rule.ActiveToUtc.Value.Month);
            if (monthStart > endMonth)
            {
                return false;
            }
        }

        var months =
            (monthStart.Year - startMonth.Year) * 12 +
            monthStart.Month - startMonth.Month;

        return rule.FrequencyCode switch
        {
            FamilyRecurringFrequencies.Once => months == 0,
            FamilyRecurringFrequencies.Monthly => true,
            FamilyRecurringFrequencies.Quarterly => months % 3 == 0,
            FamilyRecurringFrequencies.Yearly => months % 12 == 0,
            _ => false
        };
    }

    private static bool IsMonthWithinRule(
        FamilyRuleRow rule,
        DateTime monthStart)
    {
        var startMonth = MonthStart(rule.ActiveFromUtc.Year, rule.ActiveFromUtc.Month);
        var endMonth = rule.ActiveToUtc.HasValue
            ? MonthStart(rule.ActiveToUtc.Value.Year, rule.ActiveToUtc.Value.Month)
            : DateTime.MaxValue;
        return monthStart >= startMonth && monthStart <= endMonth;
    }

    private async Task<IReadOnlyList<FamilyBudgetLinkSummary>> BuildActualLinkSummariesAsync(
        IReadOnlyList<BudgetLinkRow> links,
        bool revealPersonalDetails,
        CancellationToken cancellationToken)
    {
        var result = new List<FamilyBudgetLinkSummary>();

        var personalIds = links
            .Where(x => x.SourceType == FamilyBudgetSourceTypes.PersonalTransaction)
            .Select(x => x.SourceId)
            .Distinct()
            .ToArray();

        var personalTransactions = personalIds.Length == 0
            ? Array.Empty<PersonalFinancialTransaction>()
            : await dbContext.PersonalFinancialTransactions
                .AsNoTracking()
                .Where(x =>
                    personalIds.Contains(x.Id) ||
                    (x.CorrectsTransactionId.HasValue &&
                     personalIds.Contains(x.CorrectsTransactionId.Value)))
                .ToArrayAsync(cancellationToken);

        var householdIds = links
            .Where(x => x.SourceType == FamilyBudgetSourceTypes.HouseholdEntry)
            .Select(x => x.SourceId)
            .Distinct()
            .ToArray();

        var householdEntries = householdIds.Length == 0
            ? Array.Empty<HouseholdEntry>()
            : await dbContext.HouseholdEntries
                .AsNoTracking()
                .Where(x =>
                    householdIds.Contains(x.Id) ||
                    (x.CorrectsEntryId.HasValue &&
                     householdIds.Contains(x.CorrectsEntryId.Value)))
                .ToArrayAsync(cancellationToken);

        var personNames = await GetPersonNamesAsync(
            links
                .SelectMany(x => new[] { x.PersonId, x.BeneficiaryPersonId })
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToArray(),
            cancellationToken);

        foreach (var link in links)
        {
            if (link.SourceType == FamilyBudgetSourceTypes.PersonalTransaction)
            {
                var source = personalTransactions.SingleOrDefault(x => x.Id == link.SourceId);
                if (source is null)
                {
                    continue;
                }

                var netMinor = source.AmountMinor + personalTransactions
                    .Where(x => x.CorrectsTransactionId == source.Id)
                    .Sum(x => x.AmountMinor);
                var expense = FamilyFinanceMoney.FromMinorUnits(-netMinor);
                var ownerName = link.PersonId.HasValue
                    ? personNames.GetValueOrDefault(link.PersonId.Value)
                    : null;

                result.Add(
                    BuildLinkSummary(
                        link,
                        revealPersonalDetails
                            ? BuildPrivateTransactionLabel(
                                source.CategoryCode,
                                source.Counterparty,
                                source.Description)
                            : string.IsNullOrWhiteSpace(ownerName)
                                ? "Udostępniony wydatek prywatny"
                                : $"Udostępniony wydatek · {ownerName}",
                        source.OccurredAtUtc,
                        expense,
                        personNames));
            }
            else if (link.SourceType == FamilyBudgetSourceTypes.HouseholdEntry)
            {
                var source = householdEntries.SingleOrDefault(x => x.Id == link.SourceId);
                if (source is null)
                {
                    continue;
                }

                var netMinor = source.AmountMinor + householdEntries
                    .Where(x => x.CorrectsEntryId == source.Id)
                    .Sum(x => x.AmountMinor);
                var expense = FamilyFinanceMoney.FromMinorUnits(-netMinor);

                result.Add(
                    BuildLinkSummary(
                        link,
                        BuildHouseholdEntryLabel(
                            source.CategoryCode,
                            source.Description),
                        source.OccurredAtUtc,
                        expense,
                        personNames));
            }
        }

        return result;
    }

    private async Task<IReadOnlyList<FamilyBudgetLinkSummary>> BuildLinkManagementSummariesAsync(
        IReadOnlyList<BudgetLinkRow> links,
        Guid actorPersonId,
        CancellationToken cancellationToken)
    {
        var actual = links
            .Where(x => x.SourceType != FamilyBudgetSourceTypes.PersonalRecurringRule)
            .ToArray();
        var result = (await BuildActualLinkSummariesAsync(
            actual,
            revealPersonalDetails: true,
            cancellationToken)).ToList();

        var recurringLinks = links
            .Where(x => x.SourceType == FamilyBudgetSourceTypes.PersonalRecurringRule)
            .ToArray();

        if (recurringLinks.Length == 0)
        {
            return result;
        }

        var ids = recurringLinks.Select(x => x.SourceId).Distinct().ToArray();
        var rules = await dbContext.PersonalRecurringRules
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id) && x.OwnerPersonId == actorPersonId)
            .ToArrayAsync(cancellationToken);
        var personNames = await GetPersonNamesAsync(
            recurringLinks
                .SelectMany(x => new[] { x.PersonId, x.BeneficiaryPersonId })
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToArray(),
            cancellationToken);

        foreach (var link in recurringLinks)
        {
            var rule = rules.SingleOrDefault(x => x.Id == link.SourceId);
            if (rule is null)
            {
                continue;
            }

            result.Add(
                BuildLinkSummary(
                    link,
                    $"{rule.Name} · {PersonalFinanceCategories.GetNamePl(rule.CategoryCode)}",
                    rule.StartDateUtc,
                    FamilyFinanceMoney.FromMinorUnits(rule.PlannedAmountMinor),
                    personNames));
        }

        return result
            .OrderByDescending(x => x.SourceDateUtc)
            .ThenBy(x => x.SourceLabel)
            .ToArray();
    }

    private static FamilyBudgetLinkSummary BuildLinkSummary(
        BudgetLinkRow link,
        string label,
        DateTime? sourceDate,
        decimal? amount,
        IReadOnlyDictionary<Guid, string> personNames) =>
        new(
            link.Id,
            link.SourceType,
            FamilyBudgetSourceTypes.GetNamePl(link.SourceType),
            link.SourceId,
            label,
            link.PeriodKey,
            sourceDate,
            amount,
            link.CategoryCode,
            FamilyBudgetCategories.GetNamePl(link.CategoryCode),
            link.PersonId,
            link.PersonId.HasValue
                ? personNames.GetValueOrDefault(link.PersonId.Value)
                : null,
            link.BeneficiaryPersonId,
            link.BeneficiaryPersonId.HasValue
                ? personNames.GetValueOrDefault(link.BeneficiaryPersonId.Value)
                : null);

    private async Task<IReadOnlyList<FamilyRecurringCostSummary>> GetRuleSummariesAsync(
        Guid familyGroupId,
        IReadOnlyList<MemberRow> members,
        CancellationToken cancellationToken)
    {
        var rules = await GetRuleRowsAsync(familyGroupId, cancellationToken);
        var names = members.ToDictionary(x => x.PersonId, x => x.DisplayName);

        return rules
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Name)
            .Select(x =>
                new FamilyRecurringCostSummary(
                    x.Id,
                    x.Name,
                    x.CategoryCode,
                    FamilyBudgetCategories.GetNamePl(x.CategoryCode),
                    FamilyFinanceMoney.FromMinorUnits(x.PlannedAmountMinor),
                    x.FrequencyCode,
                    FamilyRecurringFrequencies.GetNamePl(x.FrequencyCode),
                    x.DueDay,
                    x.BeneficiaryPersonId,
                    x.BeneficiaryPersonId.HasValue
                        ? names.GetValueOrDefault(x.BeneficiaryPersonId.Value)
                        : null,
                    x.ActiveFromUtc,
                    x.ActiveToUtc,
                    x.IsActive))
            .ToArray();
    }

    private async Task<IReadOnlyList<FamilyOccurrenceRow>> GetFamilyOccurrencesAsync(
        Guid familyGroupId,
        string periodKey,
        CancellationToken cancellationToken)
    {
        var result = new List<FamilyOccurrenceRow>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT o.Id, o.RuleId, o.PlannedAmountMinor,
                           r.CategoryCode, o.BeneficiaryPersonId
                    FROM FamilyRecurringOccurrences o
                    INNER JOIN FamilyRecurringRules r ON r.Id = o.RuleId
                    WHERE o.FamilyGroupId = $groupId
                      AND o.PeriodKey = $periodKey
                      AND o.StatusCode <> $cancelled
                    ORDER BY o.PlannedDateUtc, r.Name;
                    """;
                AddParameter(command, "$groupId", familyGroupId);
                AddParameter(command, "$periodKey", periodKey);
                AddParameter(command, "$cancelled", FamilyRecurringOccurrenceStatuses.Cancelled);
                AttachCurrentTransaction(command);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    result.Add(
                        new FamilyOccurrenceRow(
                            reader.GetGuid(0),
                            reader.GetGuid(1),
                            reader.GetInt64(2),
                            reader.GetString(3),
                            reader.IsDBNull(4) ? null : reader.GetGuid(4)));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<IReadOnlyList<FamilyRuleRow>> GetRuleRowsAsync(
        Guid familyGroupId,
        CancellationToken cancellationToken)
    {
        var result = new List<FamilyRuleRow>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT Id, FamilyGroupId, Name, CategoryCode, PlannedAmountMinor,
                           FrequencyCode, DueDay, BeneficiaryPersonId,
                           ActiveFromUtc, ActiveToUtc, IsActive
                    FROM FamilyRecurringRules
                    WHERE FamilyGroupId = $groupId
                    ORDER BY Name;
                    """;
                AddParameter(command, "$groupId", familyGroupId);
                AttachCurrentTransaction(command);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    result.Add(
                        new FamilyRuleRow(
                            reader.GetGuid(0),
                            reader.GetGuid(1),
                            reader.GetString(2),
                            reader.GetString(3),
                            reader.GetInt64(4),
                            reader.GetString(5),
                            reader.GetInt32(6),
                            reader.IsDBNull(7) ? null : reader.GetGuid(7),
                            ReadDateTime(reader, 8),
                            reader.IsDBNull(9) ? (DateTime?)null : ReadDateTime(reader, 9),
                            reader.GetInt64(10) != 0));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<FamilyRuleRow?> GetRuleByIdAsync(
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        FamilyRuleRow? result = null;
        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT Id, FamilyGroupId, Name, CategoryCode, PlannedAmountMinor,
                           FrequencyCode, DueDay, BeneficiaryPersonId,
                           ActiveFromUtc, ActiveToUtc, IsActive
                    FROM FamilyRecurringRules
                    WHERE Id = $id
                    LIMIT 1;
                    """;
                AddParameter(command, "$id", ruleId);
                AttachCurrentTransaction(command);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    result = new FamilyRuleRow(
                        reader.GetGuid(0),
                        reader.GetGuid(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetInt64(4),
                        reader.GetString(5),
                        reader.GetInt32(6),
                        reader.IsDBNull(7) ? null : reader.GetGuid(7),
                        ReadDateTime(reader, 8),
                        reader.IsDBNull(9) ? (DateTime?)null : ReadDateTime(reader, 9),
                        reader.GetInt64(10) != 0);
                }
            },
            cancellationToken);
        return result;
    }

    private async Task<IReadOnlyList<BudgetLinkRow>> GetActiveLinksAsync(
        Guid familyGroupId,
        CancellationToken cancellationToken)
    {
        var result = new List<BudgetLinkRow>();
        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT Id, FamilyGroupId, PeriodKey, SourceType, SourceId,
                           PersonId, CategoryCode, BeneficiaryPersonId,
                           LinkedAtUtc, UnlinkedAtUtc
                    FROM FamilyBudgetLinks
                    WHERE FamilyGroupId = $groupId
                      AND UnlinkedAtUtc IS NULL;
                    """;
                AddParameter(command, "$groupId", familyGroupId);
                AttachCurrentTransaction(command);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    result.Add(ReadBudgetLink(reader));
                }
            },
            cancellationToken);
        return result;
    }

    private async Task<BudgetLinkRow?> GetLinkBySourceAsync(
        Guid familyGroupId,
        string sourceType,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        BudgetLinkRow? result = null;
        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT Id, FamilyGroupId, PeriodKey, SourceType, SourceId,
                           PersonId, CategoryCode, BeneficiaryPersonId,
                           LinkedAtUtc, UnlinkedAtUtc
                    FROM FamilyBudgetLinks
                    WHERE FamilyGroupId = $groupId
                      AND SourceType = $sourceType
                      AND SourceId = $sourceId
                    LIMIT 1;
                    """;
                AddParameter(command, "$groupId", familyGroupId);
                AddParameter(command, "$sourceType", sourceType);
                AddParameter(command, "$sourceId", sourceId);
                AttachCurrentTransaction(command);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    result = ReadBudgetLink(reader);
                }
            },
            cancellationToken);
        return result;
    }

    private async Task<BudgetLinkRow?> GetLinkByIdAsync(
        Guid linkId,
        CancellationToken cancellationToken)
    {
        BudgetLinkRow? result = null;
        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT Id, FamilyGroupId, PeriodKey, SourceType, SourceId,
                           PersonId, CategoryCode, BeneficiaryPersonId,
                           LinkedAtUtc, UnlinkedAtUtc
                    FROM FamilyBudgetLinks
                    WHERE Id = $id
                      AND UnlinkedAtUtc IS NULL
                    LIMIT 1;
                    """;
                AddParameter(command, "$id", linkId);
                AttachCurrentTransaction(command);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    result = ReadBudgetLink(reader);
                }
            },
            cancellationToken);
        return result;
    }

    private static BudgetLinkRow ReadBudgetLink(DbDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3),
            reader.GetGuid(4),
            reader.IsDBNull(5) ? null : reader.GetGuid(5),
            reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetGuid(7),
            ReadDateTime(reader, 8),
            reader.IsDBNull(9) ? (DateTime?)null : ReadDateTime(reader, 9));

    private async Task<ActorRow> GetActorAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var actor = await (
            from account in dbContext.UserAccounts.AsNoTracking()
            join person in dbContext.People.AsNoTracking()
                on account.PersonId equals person.Id
            where account.Id == actorUserId && account.IsActive && person.IsActive
            select new
            {
                person.Id,
                person.DisplayName,
                person.FirstName,
                person.LastName
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Nie można ustalić aktywnej osoby powiązanej z kontem.");

        var householdId = await dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(x => x.PersonId == actor.Id && x.IsActive)
            .Select(x => (Guid?)x.HouseholdId)
            .FirstOrDefaultAsync(cancellationToken);

        return new ActorRow(
            actor.Id,
            BuildDisplayName(actor.DisplayName, actor.FirstName, actor.LastName),
            householdId);
    }

    private async Task<GroupRow> EnsureMembershipAsync(
        Guid familyGroupId,
        Guid personId,
        Guid? actorHouseholdId,
        CancellationToken cancellationToken)
    {
        GroupRow? result = null;
        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT g.Id, g.HouseholdId, g.Name
                    FROM FamilyGroups g
                    INNER JOIN FamilyMembers m ON m.FamilyGroupId = g.Id
                    WHERE g.Id = $groupId
                      AND g.IsActive = 1
                      AND m.PersonId = $personId
                      AND m.ValidToUtc IS NULL
                    LIMIT 1;
                    """;
                AddParameter(command, "$groupId", familyGroupId);
                AddParameter(command, "$personId", personId);
                AttachCurrentTransaction(command);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    result = new GroupRow(
                        reader.GetGuid(0),
                        reader.GetGuid(1),
                        reader.GetString(2));
                }
            },
            cancellationToken);

        if (result is null ||
            !actorHouseholdId.HasValue ||
            result.HouseholdId != actorHouseholdId.Value)
        {
            throw new UnauthorizedAccessException(
                "Dostęp do budżetu wymaga aktywnego członkostwa w tej rodzinie i gospodarstwie.");
        }

        return result;
    }

    private async Task<IReadOnlyList<MemberRow>> GetMembersAsync(
        Guid familyGroupId,
        CancellationToken cancellationToken)
    {
        var result = new List<MemberRow>();
        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT m.PersonId, m.FamilyRoleCode,
                           p.DisplayName, p.FirstName, p.LastName,
                           COALESCE(s.ShareFamilyExpenses, 0),
                           COALESCE(s.ShareRecurringRules, 0)
                    FROM FamilyMembers m
                    INNER JOIN People p ON p.Id = m.PersonId
                    LEFT JOIN FamilySharingPolicies s
                      ON s.FamilyGroupId = m.FamilyGroupId
                     AND s.PersonId = m.PersonId
                     AND s.EffectiveToUtc IS NULL
                    WHERE m.FamilyGroupId = $groupId
                      AND m.ValidToUtc IS NULL
                      AND p.IsActive = 1;
                    """;
                AddParameter(command, "$groupId", familyGroupId);
                AttachCurrentTransaction(command);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    result.Add(
                        new MemberRow(
                            reader.GetGuid(0),
                            reader.GetString(1),
                            BuildDisplayName(
                                reader.IsDBNull(2) ? null : reader.GetString(2),
                                reader.GetString(3),
                                reader.GetString(4)),
                            reader.GetInt64(5) != 0,
                            reader.GetInt64(6) != 0));
                }
            },
            cancellationToken);
        return result;
    }

    private async Task EnsureBeneficiaryAsync(
        Guid familyGroupId,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var count = await ScalarLongAsync(
            """
            SELECT COUNT(1)
            FROM FamilyMembers
            WHERE FamilyGroupId = $groupId
              AND PersonId = $personId
              AND ValidToUtc IS NULL;
            """,
            [P("$groupId", familyGroupId), P("$personId", personId)],
            cancellationToken);

        if (count == 0)
        {
            throw new InvalidOperationException(
                "Beneficjent kosztu musi być aktywnym członkiem tej rodziny.");
        }
    }

    private async Task<IReadOnlyDictionary<Guid, string>> GetPersonNamesAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var rows = await dbContext.People
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.DisplayName,
                x.FirstName,
                x.LastName
            })
            .ToArrayAsync(cancellationToken);

        return rows.ToDictionary(
            x => x.Id,
            x => BuildDisplayName(x.DisplayName, x.FirstName, x.LastName));
    }

    private async Task<long> ScalarLongAsync(
        string sql,
        IReadOnlyList<ParameterValue> parameters,
        CancellationToken cancellationToken)
    {
        long result = 0;
        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                foreach (var parameter in parameters)
                {
                    AddParameter(command, parameter.Name, parameter.Value);
                }
                AttachCurrentTransaction(command);
                result = Convert.ToInt64(
                    await command.ExecuteScalarAsync(cancellationToken) ?? 0);
            },
            cancellationToken);
        return result;
    }

    private async Task<int> ExecuteAsync(
        string sql,
        IReadOnlyList<ParameterValue> parameters,
        CancellationToken cancellationToken)
    {
        var result = 0;
        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                foreach (var parameter in parameters)
                {
                    AddParameter(command, parameter.Name, parameter.Value);
                }
                AttachCurrentTransaction(command);
                result = await command.ExecuteNonQueryAsync(cancellationToken);
            },
            cancellationToken);
        return result;
    }

    private async Task WithConnectionAsync(
        Func<DbConnection, Task> action,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await action(connection);
        }
        finally
        {
            if (shouldClose && dbContext.Database.CurrentTransaction is null)
            {
                await connection.CloseAsync();
            }
        }
    }

    private void AttachCurrentTransaction(DbCommand command)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            command.Transaction = dbContext.Database.CurrentTransaction.GetDbTransaction();
        }
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static ParameterValue P(string name, object? value) => new(name, value);

    private static decimal SumMajor(IEnumerable<BudgetAmountItem> items) =>
        FamilyFinanceMoney.FromMinorUnits(items.Sum(x => x.AmountMinor));

    private static DateTime MonthStart(int year, int month) =>
        new(year, month, 1, 0, 0, 0, DateTimeKind.Utc);

    private static string BuildPeriodKey(int year, int month) =>
        $"{year:D4}-{month:D2}";

    private static DateTime NormalizeUtcDate(DateTime value) =>
        DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    private static DateTime ReadDateTime(DbDataReader reader, int ordinal)
    {
        var value = reader.GetValue(ordinal);
        if (value is DateTime dateTime)
        {
            return dateTime.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
                : dateTime.ToUniversalTime();
        }

        var parsed = Convert.ToDateTime(value);
        return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
    }

    private static string NormalizeRequiredText(
        string? value,
        string fieldName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} nie może być pusta.");
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"{fieldName} może mieć maksymalnie {maxLength} znaków.");
        }

        return normalized;
    }

    private static string BuildDisplayName(
        string? displayName,
        string firstName,
        string lastName) =>
        string.IsNullOrWhiteSpace(displayName)
            ? $"{firstName} {lastName}".Trim()
            : displayName.Trim();

    private static string BuildPrivateTransactionLabel(
        string? categoryCode,
        string? counterparty,
        string? description)
    {
        var parts = new[]
        {
            PersonalFinanceCategories.GetNamePl(categoryCode),
            counterparty,
            description
        }
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x!.Trim())
        .Distinct()
        .Take(3);

        return string.Join(" · ", parts);
    }

    private static string BuildHouseholdEntryLabel(
        string? categoryCode,
        string? description)
    {
        var category = HouseholdFinanceCategories.GetNamePl(categoryCode);
        return string.IsNullOrWhiteSpace(description)
            ? category
            : $"{category} · {description.Trim()}";
    }

    private static void ValidatePeriod(int year, int month)
    {
        if (year < 2000 || year > 2100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(year),
                "Rok musi mieścić się w zakresie 2000-2100.");
        }

        if (month < 1 || month > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(month),
                "Miesiąc musi mieścić się w zakresie 1-12.");
        }
    }

    private sealed record ActorRow(
        Guid PersonId,
        string DisplayName,
        Guid? HouseholdId);

    private sealed record GroupRow(
        Guid FamilyGroupId,
        Guid HouseholdId,
        string Name);

    private sealed record MemberRow(
        Guid PersonId,
        string FamilyRoleCode,
        string DisplayName,
        bool ShareFamilyExpenses,
        bool ShareRecurringRules);

    private sealed record FamilyRuleRow(
        Guid Id,
        Guid FamilyGroupId,
        string Name,
        string CategoryCode,
        long PlannedAmountMinor,
        string FrequencyCode,
        int DueDay,
        Guid? BeneficiaryPersonId,
        DateTime ActiveFromUtc,
        DateTime? ActiveToUtc,
        bool IsActive);

    private sealed record FamilyOccurrenceRow(
        Guid Id,
        Guid RuleId,
        long PlannedAmountMinor,
        string CategoryCode,
        Guid? BeneficiaryPersonId);

    private sealed record BudgetLinkRow(
        Guid Id,
        Guid FamilyGroupId,
        string? PeriodKey,
        string SourceType,
        Guid SourceId,
        Guid? PersonId,
        string CategoryCode,
        Guid? BeneficiaryPersonId,
        DateTime LinkedAtUtc,
        DateTime? UnlinkedAtUtc);

    private sealed record BudgetAmountItem(
        string CategoryCode,
        long AmountMinor,
        Guid? PersonId,
        Guid? BeneficiaryPersonId);

    private sealed record ParameterValue(
        string Name,
        object? Value);
}
