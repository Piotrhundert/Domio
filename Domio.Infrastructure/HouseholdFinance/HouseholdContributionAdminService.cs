using Domio.Application.Auditing;
using Domio.Application.HouseholdFinance;
using Domio.Domain.HouseholdFinance;
using Domio.Domain.PersonalFinance;
using Domio.Domain.Users;
using Domio.Infrastructure.Authorization;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.Infrastructure.HouseholdFinance;

public sealed class HouseholdContributionAdminService(
    DomioDbContext dbContext,
    IAuditService auditService) : IHouseholdContributionAdminService
{
    public async Task<HouseholdContributionAdminOverview?> GetOverviewAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinanceHouseholdManage,
            cancellationToken);

        var context = await GetActorHouseholdAsync(
            actorUserId,
            cancellationToken);

        if (context is null)
        {
            return null;
        }

        await EnsureChildContributionObligationsAsync(
            context.Household.Id,
            DateTime.UtcNow.Date.AddMonths(24),
            cancellationToken);

        var rules =
            await (
                from rule in dbContext.HouseholdContributionRules
                    .AsNoTracking()
                join membership in dbContext.HouseholdMembers
                    .AsNoTracking()
                    on rule.HouseholdMemberId equals membership.Id
                join person in dbContext.People
                    .AsNoTracking()
                    on membership.PersonId equals person.Id
                join account in dbContext.HouseholdAccounts
                    .AsNoTracking()
                    on rule.TargetHouseholdAccountId equals account.Id
                where
                    rule.HouseholdId == context.Household.Id &&
                    rule.IsActive &&
                    membership.IsActive &&
                    person.IsActive
                orderby person.LastName, person.FirstName
                select new
                {
                    Rule = rule,
                    Person = person,
                    Account = account
                })
                .ToArrayAsync(cancellationToken);

        var activeRules =
            rules
                .Select(x =>
                    new HouseholdContributionAdminRuleItem(
                        x.Rule.Id,
                        x.Rule.HouseholdMemberId,
                        GetPersonDisplayName(x.Person),
                        x.Rule.ModeCode,
                        HouseholdContributionModes.GetNamePl(
                            x.Rule.ModeCode),
                        x.Rule.FixedAmountMinor.HasValue
                            ? HouseholdFinanceMoney.FromMinorUnits(
                                x.Rule.FixedAmountMinor.Value)
                            : null,
                        x.Rule.PercentageBasisPoints.HasValue
                            ? HouseholdContributionMath.FromBasisPoints(
                                x.Rule.PercentageBasisPoints.Value)
                            : null,
                        x.Account.Name,
                        x.Rule.ValidFromUtc,
                        x.Rule.ValidToUtc,
                        x.Rule.DueOffsetDays,
                        x.Rule.ReminderDays,
                        x.Person.PersonTypeCode == PersonTypes.Child))
                .ToArray();

        var childRows =
            await (
                from childMembership in dbContext.HouseholdMembers
                    .AsNoTracking()
                join childPerson in dbContext.People
                    .AsNoTracking()
                    on childMembership.PersonId equals childPerson.Id
                where
                    childMembership.HouseholdId == context.Household.Id &&
                    childMembership.IsActive &&
                    childPerson.IsActive &&
                    childPerson.PersonTypeCode == PersonTypes.Child &&
                    !dbContext.UserAccounts.Any(userAccount =>
                        userAccount.PersonId == childPerson.Id &&
                        userAccount.IsActive)
                orderby childPerson.FirstName, childPerson.LastName
                select new
                {
                    Membership = childMembership,
                    Person = childPerson
                })
                .ToArrayAsync(cancellationToken);

        var children =
            childRows
                .Select(childRow =>
                {
                    var activeChildRule =
                        rules.FirstOrDefault(ruleRow =>
                            ruleRow.Rule.HouseholdMemberId ==
                                childRow.Membership.Id &&
                            ruleRow.Person.PersonTypeCode ==
                                PersonTypes.Child);

                    return new HouseholdChildMemberItem(
                        childRow.Membership.Id,
                        childRow.Person.Id,
                        GetPersonDisplayName(childRow.Person),
                        activeChildRule?.Rule.Id,
                        activeChildRule?.Rule.FixedAmountMinor is long amountMinor
                            ? HouseholdFinanceMoney.FromMinorUnits(amountMinor)
                            : null,
                        activeChildRule?.Rule.DueOffsetDays,
                        activeChildRule?.Account.Name);
                })
                .ToArray();

        return new HouseholdContributionAdminOverview(
            context.Household.Id,
            context.Household.Name,
            context.Household.CurrencyCode,
            activeRules,
            children);
    }

    public async Task<HouseholdContributionRuleEditData?> GetRuleForEditAsync(
        Guid ruleId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinanceHouseholdManage,
            cancellationToken);

        var context = await GetActorHouseholdAsync(
            actorUserId,
            cancellationToken);

        if (context is null)
        {
            return null;
        }

        var row =
            await (
                from rule in dbContext.HouseholdContributionRules
                    .AsNoTracking()
                join membership in dbContext.HouseholdMembers
                    .AsNoTracking()
                    on rule.HouseholdMemberId equals membership.Id
                join person in dbContext.People
                    .AsNoTracking()
                    on membership.PersonId equals person.Id
                join incomeRule in dbContext.PersonalRecurringRules
                    .AsNoTracking()
                    on rule.IncomeRuleId equals (Guid?)incomeRule.Id
                    into incomeJoin
                from incomeRule in incomeJoin.DefaultIfEmpty()
                where
                    rule.Id == ruleId &&
                    rule.HouseholdId == context.Household.Id &&
                    rule.IsActive
                select new
                {
                    Rule = rule,
                    Person = person,
                    IncomeRule = incomeRule
                })
                .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var targetAccounts =
            await dbContext.HouseholdAccounts
                .AsNoTracking()
                .Where(x =>
                    x.HouseholdId == context.Household.Id &&
                    x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x =>
                    new HouseholdContributionTargetAccountOption(
                        x.Id,
                        x.Name,
                        x.CurrencyCode))
                .ToArrayAsync(cancellationToken);

        return new HouseholdContributionRuleEditData(
            row.Rule.Id,
            GetPersonDisplayName(row.Person),
            row.Rule.ModeCode,
            row.Rule.FixedAmountMinor.HasValue
                ? HouseholdFinanceMoney.FromMinorUnits(
                    row.Rule.FixedAmountMinor.Value)
                : null,
            row.Rule.PercentageBasisPoints.HasValue
                ? HouseholdContributionMath.FromBasisPoints(
                    row.Rule.PercentageBasisPoints.Value)
                : null,
            row.Rule.DueOffsetDays,
            row.Rule.TargetHouseholdAccountId,
            row.Rule.ValidFromUtc,
            row.Rule.ValidToUtc,
            row.Rule.ReminderDays,
            row.IncomeRule?.Name,
            row.IncomeRule is not null
                ? PersonalFinanceMoney.FromMinorUnits(
                    row.IncomeRule.PlannedAmountMinor)
                : null,
            context.Household.CurrencyCode,
            row.Person.PersonTypeCode == PersonTypes.Child &&
                row.Rule.ModeCode == HouseholdContributionModes.FixedAmount &&
                !row.Rule.IncomeRuleId.HasValue,
            targetAccounts);
    }

    public async Task<UpdateHouseholdContributionRuleResult> UpdateRuleAsync(
        UpdateHouseholdContributionRuleRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinanceHouseholdManage,
            cancellationToken);

        var context = await GetActorHouseholdAsync(
            actorUserId,
            cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Użytkownik nie jest przypisany do aktywnego gospodarstwa.");

        if (!HouseholdContributionModes.IsValid(request.ModeCode))
        {
            throw new ArgumentException(
                "Wybrano nieprawidłowy sposób naliczania składki.");
        }

        if (request.DueOffsetDays < 0 || request.DueOffsetDays > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.DueOffsetDays),
                "Termin składki musi mieścić się w zakresie 0-31 dni od planowanej wypłaty.");
        }

        if (request.ReminderDays < 0 || request.ReminderDays > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.ReminderDays),
                "Przypomnienie musi mieścić się w zakresie 0-31 dni.");
        }

        long? fixedAmountMinor = null;
        int? percentageBasisPoints = null;

        if (request.ModeCode == HouseholdContributionModes.FixedAmount)
        {
            if (!request.FixedAmount.HasValue || request.FixedAmount.Value <= 0)
            {
                throw new ArgumentException(
                    "Dla stałej składki podaj kwotę większą od zera.");
            }

            fixedAmountMinor =
                HouseholdFinanceMoney.ToMinorUnits(
                    request.FixedAmount.Value);
        }
        else
        {
            if (!request.Percentage.HasValue)
            {
                throw new ArgumentException(
                    "Dla składki procentowej podaj procent planowanego wynagrodzenia.");
            }

            percentageBasisPoints =
                HouseholdContributionMath.ToBasisPoints(
                    request.Percentage.Value);
        }

        var oldRule =
            await dbContext.HouseholdContributionRules
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == request.RuleId &&
                        x.HouseholdId == context.Household.Id &&
                        x.IsActive,
                    cancellationToken)
            ?? throw new InvalidOperationException(
                "Aktywna reguła składki nie istnieje albo została już zmieniona.");

        var targetAccount =
            await dbContext.HouseholdAccounts
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == request.TargetHouseholdAccountId &&
                        x.HouseholdId == context.Household.Id &&
                        x.IsActive,
                    cancellationToken)
            ?? throw new ArgumentException(
                "Wybrane konto docelowe gospodarstwa nie istnieje lub jest nieaktywne.");

        var memberRow =
            await (
                from activeMembership in dbContext.HouseholdMembers
                    .AsNoTracking()
                join activePerson in dbContext.People
                    .AsNoTracking()
                    on activeMembership.PersonId equals activePerson.Id
                where
                    activeMembership.Id == oldRule.HouseholdMemberId &&
                    activeMembership.HouseholdId == context.Household.Id &&
                    activeMembership.IsActive &&
                    activePerson.IsActive
                select new
                {
                    Membership = activeMembership,
                    Person = activePerson
                })
                .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Domownik przypisany do reguły nie jest już aktywnym członkiem gospodarstwa.");

        var isChildContribution =
            memberRow.Person.PersonTypeCode == PersonTypes.Child &&
            !oldRule.IncomeRuleId.HasValue;

        if (isChildContribution &&
            request.ModeCode != HouseholdContributionModes.FixedAmount)
        {
            throw new ArgumentException(
                "Dla dziecka bez konta można ustawić wyłącznie stałą składkę miesięczną.");
        }

        if (isChildContribution &&
            (request.DueOffsetDays < 1 || request.DueOffsetDays > 28))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.DueOffsetDays),
                "Dla składki dziecka wybierz dzień miesiąca od 1 do 28.");
        }

        PersonalRecurringRule[] salaryRules =
            isChildContribution
                ? []
                : await dbContext.PersonalRecurringRules
                    .AsNoTracking()
                    .Where(x =>
                        x.OwnerPersonId == memberRow.Membership.PersonId &&
                        x.IsActive &&
                        x.KindCode == PersonalTransactionKinds.Income &&
                        x.FrequencyCode == PersonalRecurringFrequencies.Monthly &&
                        x.CategoryCode == PersonalFinanceCategories.Salary)
                    .OrderBy(x => x.CreatedAtUtc)
                    .ToArrayAsync(cancellationToken);

        PersonalRecurringRule? salaryRule =
            salaryRules.Length == 1
                ? salaryRules[0]
                : null;

        if (salaryRule is not null &&
            !string.Equals(
                salaryRule.CurrencyCode,
                targetAccount.CurrencyCode,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Waluta planowanego wynagrodzenia nie jest zgodna z walutą wybranego konta domu.");
        }

        var now = DateTime.UtcNow;
        var currentMonth =
            new DateTime(
                now.Year,
                now.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var oldStartMonth =
            new DateTime(
                oldRule.ValidFromUtc.Year,
                oldRule.ValidFromUtc.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var baseEffectiveMonth =
            oldStartMonth > currentMonth
                ? oldStartMonth
                : currentMonth;

        var obligations =
            await dbContext.HouseholdContributionObligations
                .Where(x =>
                    x.ContributionRuleId == oldRule.Id &&
                    x.HouseholdId == context.Household.Id)
                .ToArrayAsync(cancellationToken);

        var obligationIds =
            obligations.Select(x => x.Id).ToArray();

        var pendingPaymentObligationIds =
            obligationIds.Length == 0
                ? new HashSet<Guid>()
                : (await dbContext.HouseholdContributionPaymentRequests
                    .AsNoTracking()
                    .Where(x =>
                        obligationIds.Contains(x.ObligationId) &&
                        x.StatusCode == HouseholdContributionPaymentStatuses.Pending)
                    .Select(x => x.ObligationId)
                    .ToArrayAsync(cancellationToken))
                    .ToHashSet();

        DateTime? latestLockedMonth = null;

        foreach (var obligation in obligations)
        {
            var periodMonth = ParsePeriodMonth(obligation.PeriodKey);

            if (periodMonth is null || periodMonth.Value < baseEffectiveMonth)
            {
                continue;
            }

            var locked =
                obligation.PaidAmountMinor > 0 ||
                pendingPaymentObligationIds.Contains(obligation.Id);

            if (!locked)
            {
                continue;
            }

            if (!latestLockedMonth.HasValue ||
                periodMonth.Value > latestLockedMonth.Value)
            {
                latestLockedMonth = periodMonth.Value;
            }
        }

        var effectiveFromUtc =
            latestLockedMonth.HasValue
                ? latestLockedMonth.Value.AddMonths(1)
                : baseEffectiveMonth;

        DateTime? validToUtc =
            request.ValidToUtc.HasValue
                ? DateTime.SpecifyKind(
                    request.ValidToUtc.Value.Date,
                    DateTimeKind.Utc)
                : null;

        if (validToUtc.HasValue &&
            validToUtc.Value < effectiveFromUtc)
        {
            throw new ArgumentException(
                $"Data końcowa nowej reguły nie może być wcześniejsza niż {effectiveFromUtc:dd.MM.yyyy}.");
        }

        var newRule =
            new HouseholdContributionRule
            {
                Id = Guid.NewGuid(),
                HouseholdId = oldRule.HouseholdId,
                HouseholdMemberId = oldRule.HouseholdMemberId,
                ModeCode = request.ModeCode,
                FixedAmountMinor = fixedAmountMinor,
                PercentageBasisPoints = percentageBasisPoints,
                IncomeRuleId = isChildContribution
                    ? null
                    : salaryRule?.Id,
                DueOffsetDays = request.DueOffsetDays,
                TargetHouseholdAccountId = targetAccount.Id,
                ValidFromUtc = effectiveFromUtc,
                ValidToUtc = validToUtc,
                ReminderDays = request.ReminderDays,
                IsActive = true,
                CreatedByUserId = actorUserId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

        var previousMonthEnd = effectiveFromUtc.AddDays(-1);

        oldRule.ValidToUtc =
            previousMonthEnd >= oldRule.ValidFromUtc.Date
                ? previousMonthEnd
                : oldRule.ValidFromUtc.Date;
        oldRule.IsActive = false;
        oldRule.UpdatedAtUtc = now;

        var recalculated = 0;
        var cancelled = 0;

        foreach (var obligation in obligations)
        {
            var periodMonth = ParsePeriodMonth(obligation.PeriodKey);

            if (periodMonth is null || periodMonth.Value < effectiveFromUtc)
            {
                continue;
            }

            if (obligation.PaidAmountMinor > 0 ||
                pendingPaymentObligationIds.Contains(obligation.Id) ||
                obligation.StatusCode == HouseholdContributionStatuses.Paid ||
                obligation.StatusCode == HouseholdContributionStatuses.Cancelled ||
                obligation.StatusCode == HouseholdContributionStatuses.Corrected)
            {
                continue;
            }

            if (validToUtc.HasValue)
            {
                var validToMonth =
                    new DateTime(
                        validToUtc.Value.Year,
                        validToUtc.Value.Month,
                        1,
                        0,
                        0,
                        0,
                        DateTimeKind.Utc);

                if (periodMonth.Value > validToMonth)
                {
                    obligation.StatusCode = HouseholdContributionStatuses.Cancelled;
                    obligation.UpdatedAtUtc = now;
                    cancelled++;
                    continue;
                }
            }

            long amountMinor;

            if (request.ModeCode == HouseholdContributionModes.FixedAmount)
            {
                amountMinor = fixedAmountMinor!.Value;
            }
            else
            {
                if (!obligation.PlannedIncomeAmountMinor.HasValue)
                {
                    throw new InvalidOperationException(
                        $"Nie można przeliczyć składki za {obligation.PeriodKey}, ponieważ brak zapisanej planowanej podstawy wynagrodzenia.");
                }

                amountMinor =
                    HouseholdContributionMath.CalculatePercentageAmountMinor(
                        obligation.PlannedIncomeAmountMinor.Value,
                        percentageBasisPoints!.Value);
            }

            DateTime recalculatedDueDate;

            if (isChildContribution)
            {
                recalculatedDueDate =
                    new DateTime(
                        periodMonth.Value.Year,
                        periodMonth.Value.Month,
                        request.DueOffsetDays,
                        0,
                        0,
                        0,
                        DateTimeKind.Utc);
            }
            else
            {
                var plannedPayDate =
                    obligation.DueDateUtc.Date.AddDays(
                        -oldRule.DueOffsetDays);

                recalculatedDueDate =
                    DateTime.SpecifyKind(
                        plannedPayDate.AddDays(request.DueOffsetDays),
                        DateTimeKind.Utc);
            }

            obligation.ContributionRuleId = newRule.Id;
            obligation.TargetHouseholdAccountId = targetAccount.Id;
            obligation.AmountMinor = amountMinor;
            obligation.ModeCode = request.ModeCode;
            obligation.FixedAmountMinorSnapshot = fixedAmountMinor;
            obligation.PercentageBasisPointsSnapshot = percentageBasisPoints;
            obligation.IncomeRuleId = isChildContribution
                ? null
                : salaryRule?.Id ?? obligation.IncomeRuleId;
            obligation.IncomeOccurrenceId = isChildContribution
                ? null
                : obligation.IncomeOccurrenceId;
            obligation.PlannedIncomeAmountMinor = isChildContribution
                ? null
                : obligation.PlannedIncomeAmountMinor;
            obligation.DueDateUtc = recalculatedDueDate;
            obligation.StatusCode = HouseholdContributionStatuses.Pending;
            obligation.UpdatedAtUtc = now;
            recalculated++;
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        dbContext.HouseholdContributionRules.Add(newRule);

        await dbContext.SaveChangesAsync(cancellationToken);

        if (isChildContribution)
        {
            await EnsureChildContributionObligationsAsync(
                context.Household.Id,
                DateTime.UtcNow.Date.AddMonths(24),
                cancellationToken);
        }

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.HouseholdContributionRuleUpdated",
                EntityType: "HouseholdContributionRule",
                EntityId: newRule.Id.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    $"Zmieniono aktywną regułę składki. Nowa wersja obowiązuje od {effectiveFromUtc:yyyy-MM}. Historia opłaconych okresów nie została zmieniona."),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new UpdateHouseholdContributionRuleResult(
            oldRule.Id,
            newRule.Id,
            effectiveFromUtc,
            recalculated,
            cancelled);
    }

    public async Task<Guid> CreateChildAsync(
        CreateHouseholdChildRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinanceHouseholdManage,
            cancellationToken);

        var context = await GetActorHouseholdAsync(
            actorUserId,
            cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Użytkownik nie jest przypisany do aktywnego gospodarstwa.");

        var firstName = NormalizeRequiredText(
            request.FirstName,
            "Imię",
            100);

        var lastName = NormalizeRequiredText(
            request.LastName,
            "Nazwisko",
            100);

        var displayName = NormalizeOptionalText(
            request.DisplayName,
            200);

        var duplicate =
            await (
                from candidateMembership in dbContext.HouseholdMembers.AsNoTracking()
                join candidatePerson in dbContext.People.AsNoTracking()
                    on candidateMembership.PersonId equals candidatePerson.Id
                where
                    candidateMembership.HouseholdId == context.Household.Id &&
                    candidateMembership.IsActive &&
                    candidatePerson.IsActive &&
                    candidatePerson.PersonTypeCode == PersonTypes.Child &&
                    candidatePerson.FirstName.ToUpper() == firstName.ToUpper() &&
                    candidatePerson.LastName.ToUpper() == lastName.ToUpper()
                select candidatePerson.Id)
                .AnyAsync(cancellationToken);

        if (duplicate)
        {
            throw new InvalidOperationException(
                "Dziecko o takim imieniu i nazwisku jest już aktywnym członkiem gospodarstwa.");
        }

        var now = DateTime.UtcNow;
        var person =
            new Person
            {
                Id = Guid.NewGuid(),
                FirstName = firstName,
                LastName = lastName,
                DisplayName = displayName,
                PersonTypeCode = PersonTypes.Child,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

        var membership =
            new HouseholdMember
            {
                Id = Guid.NewGuid(),
                HouseholdId = context.Household.Id,
                PersonId = person.Id,
                IsActive = true,
                JoinedAtUtc = now
            };

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        dbContext.People.Add(person);
        dbContext.HouseholdMembers.Add(membership);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.HouseholdChildAdded",
                EntityType: "HouseholdMember",
                EntityId: membership.Id.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Dodano dziecko do gospodarstwa bez tworzenia konta użytkownika. Dane osobowe nie są zapisywane w opisie audytu."),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return membership.Id;
    }

    public async Task<HouseholdChildContributionFormData?> GetChildContributionFormAsync(
        Guid householdMemberId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinanceHouseholdManage,
            cancellationToken);

        var context = await GetActorHouseholdAsync(
            actorUserId,
            cancellationToken);

        if (context is null)
        {
            return null;
        }

        var child =
            await (
                from childMembership in dbContext.HouseholdMembers
                    .AsNoTracking()
                join childPerson in dbContext.People
                    .AsNoTracking()
                    on childMembership.PersonId equals childPerson.Id
                where
                    childMembership.Id == householdMemberId &&
                    childMembership.HouseholdId == context.Household.Id &&
                    childMembership.IsActive &&
                    childPerson.IsActive &&
                    childPerson.PersonTypeCode == PersonTypes.Child &&
                    !dbContext.UserAccounts.Any(userAccount =>
                        userAccount.PersonId == childPerson.Id &&
                        userAccount.IsActive)
                select new
                {
                    Membership = childMembership,
                    Person = childPerson
                })
                .SingleOrDefaultAsync(cancellationToken);

        if (child is null)
        {
            return null;
        }

        var alreadyHasActiveRule =
            await dbContext.HouseholdContributionRules
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.HouseholdId == context.Household.Id &&
                        x.HouseholdMemberId == householdMemberId &&
                        x.IsActive,
                    cancellationToken);

        if (alreadyHasActiveRule)
        {
            return null;
        }

        var targetAccounts =
            await dbContext.HouseholdAccounts
                .AsNoTracking()
                .Where(x =>
                    x.HouseholdId == context.Household.Id &&
                    x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x =>
                    new HouseholdContributionTargetAccountOption(
                        x.Id,
                        x.Name,
                        x.CurrencyCode))
                .ToArrayAsync(cancellationToken);

        return new HouseholdChildContributionFormData(
            child.Membership.Id,
            GetPersonDisplayName(child.Person),
            context.Household.CurrencyCode,
            targetAccounts);
    }

    public async Task<Guid> CreateChildContributionAsync(
        CreateHouseholdChildContributionRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinanceHouseholdManage,
            cancellationToken);

        var context = await GetActorHouseholdAsync(
            actorUserId,
            cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Użytkownik nie jest przypisany do aktywnego gospodarstwa.");

        if (request.FixedAmount <= 0)
        {
            throw new ArgumentException(
                "Kwota składki dziecka musi być większa od zera.");
        }

        if (request.DueDay < 1 || request.DueDay > 28)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.DueDay),
                "Dzień terminu składki musi mieścić się w zakresie 1-28.");
        }

        if (request.ReminderDays < 0 || request.ReminderDays > 28)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.ReminderDays),
                "Przypomnienie musi mieścić się w zakresie 0-28 dni.");
        }

        var validFromUtc =
            DateTime.SpecifyKind(
                request.ValidFromUtc.Date,
                DateTimeKind.Utc);

        DateTime? validToUtc =
            request.ValidToUtc.HasValue
                ? DateTime.SpecifyKind(
                    request.ValidToUtc.Value.Date,
                    DateTimeKind.Utc)
                : null;

        if (validToUtc.HasValue && validToUtc.Value < validFromUtc)
        {
            throw new ArgumentException(
                "Data końcowa składki nie może być wcześniejsza od daty początkowej.");
        }

        var child =
            await (
                from childMembership in dbContext.HouseholdMembers
                join childPerson in dbContext.People
                    on childMembership.PersonId equals childPerson.Id
                where
                    childMembership.Id == request.HouseholdMemberId &&
                    childMembership.HouseholdId == context.Household.Id &&
                    childMembership.IsActive &&
                    childPerson.IsActive &&
                    childPerson.PersonTypeCode == PersonTypes.Child &&
                    !dbContext.UserAccounts.Any(userAccount =>
                        userAccount.PersonId == childPerson.Id &&
                        userAccount.IsActive)
                select new
                {
                    Membership = childMembership,
                    Person = childPerson
                })
                .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Wybrane dziecko nie jest aktywnym dzieckiem bez konta użytkownika w tym gospodarstwie.");

        var activeRuleExists =
            await dbContext.HouseholdContributionRules
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.HouseholdId == context.Household.Id &&
                        x.HouseholdMemberId == child.Membership.Id &&
                        x.IsActive,
                    cancellationToken);

        if (activeRuleExists)
        {
            throw new InvalidOperationException(
                "To dziecko ma już aktywną regułę składki. Użyj edycji istniejącej reguły.");
        }

        var targetAccount =
            await dbContext.HouseholdAccounts
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == request.TargetHouseholdAccountId &&
                        x.HouseholdId == context.Household.Id &&
                        x.IsActive,
                    cancellationToken)
            ?? throw new ArgumentException(
                "Wybrane konto docelowe gospodarstwa nie istnieje lub jest nieaktywne.");

        if (!string.Equals(
                targetAccount.CurrencyCode,
                context.Household.CurrencyCode,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Waluta konta docelowego nie jest zgodna z walutą gospodarstwa.");
        }

        var now = DateTime.UtcNow;
        var rule =
            new HouseholdContributionRule
            {
                Id = Guid.NewGuid(),
                HouseholdId = context.Household.Id,
                HouseholdMemberId = child.Membership.Id,
                ModeCode = HouseholdContributionModes.FixedAmount,
                FixedAmountMinor = HouseholdFinanceMoney.ToMinorUnits(
                    request.FixedAmount),
                PercentageBasisPoints = null,
                IncomeRuleId = null,
                // Dla dziecka bez konta pole przechowuje dzień miesiąca 1-28.
                DueOffsetDays = request.DueDay,
                TargetHouseholdAccountId = targetAccount.Id,
                ValidFromUtc = validFromUtc,
                ValidToUtc = validToUtc,
                ReminderDays = request.ReminderDays,
                IsActive = true,
                CreatedByUserId = actorUserId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        dbContext.HouseholdContributionRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);

        await EnsureChildContributionObligationsAsync(
            context.Household.Id,
            DateTime.UtcNow.Date.AddMonths(24),
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.HouseholdChildContributionCreated",
                EntityType: "HouseholdContributionRule",
                EntityId: rule.Id.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Dodano stałą miesięczną składkę dla dziecka bez konta użytkownika. Kwota nie jest zapisywana w opisie audytu."),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return rule.Id;
    }

    private async Task EnsureChildContributionObligationsAsync(
        Guid householdId,
        DateTime generateThroughUtc,
        CancellationToken cancellationToken)
    {
        var childRules =
            await (
                from childRule in dbContext.HouseholdContributionRules
                join childMembership in dbContext.HouseholdMembers
                    on childRule.HouseholdMemberId equals childMembership.Id
                join childPerson in dbContext.People
                    on childMembership.PersonId equals childPerson.Id
                where
                    childRule.HouseholdId == householdId &&
                    childRule.IsActive &&
                    childRule.ModeCode == HouseholdContributionModes.FixedAmount &&
                    !childRule.IncomeRuleId.HasValue &&
                    childMembership.IsActive &&
                    childPerson.IsActive &&
                    childPerson.PersonTypeCode == PersonTypes.Child
                select childRule)
                .ToArrayAsync(cancellationToken);

        if (childRules.Length == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var currentMonth =
            new DateTime(
                now.Year,
                now.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var throughMonth =
            new DateTime(
                generateThroughUtc.Year,
                generateThroughUtc.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        foreach (var currentRule in childRules)
        {
            if (!currentRule.FixedAmountMinor.HasValue ||
                currentRule.DueOffsetDays < 1 ||
                currentRule.DueOffsetDays > 28)
            {
                continue;
            }

            var validFromMonth =
                new DateTime(
                    currentRule.ValidFromUtc.Year,
                    currentRule.ValidFromUtc.Month,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc);

            var firstMonth =
                validFromMonth > currentMonth
                    ? validFromMonth
                    : currentMonth;

            DateTime? validToMonth =
                currentRule.ValidToUtc.HasValue
                    ? new DateTime(
                        currentRule.ValidToUtc.Value.Year,
                        currentRule.ValidToUtc.Value.Month,
                        1,
                        0,
                        0,
                        0,
                        DateTimeKind.Utc)
                    : null;

            var existingPeriods =
                (await dbContext.HouseholdContributionObligations
                    .AsNoTracking()
                    .Where(x => x.ContributionRuleId == currentRule.Id)
                    .Select(x => x.PeriodKey)
                    .ToArrayAsync(cancellationToken))
                    .ToHashSet(StringComparer.Ordinal);

            for (var month = firstMonth;
                 month <= throughMonth;
                 month = month.AddMonths(1))
            {
                if (validToMonth.HasValue && month > validToMonth.Value)
                {
                    break;
                }

                var dueDateUtc =
                    new DateTime(
                        month.Year,
                        month.Month,
                        currentRule.DueOffsetDays,
                        0,
                        0,
                        0,
                        DateTimeKind.Utc);

                if (dueDateUtc.Date < currentRule.ValidFromUtc.Date ||
                    (currentRule.ValidToUtc.HasValue &&
                     dueDateUtc.Date > currentRule.ValidToUtc.Value.Date))
                {
                    continue;
                }

                var periodKey = $"{month:yyyy-MM}";

                if (!existingPeriods.Add(periodKey))
                {
                    continue;
                }

                dbContext.HouseholdContributionObligations.Add(
                    new HouseholdContributionObligation
                    {
                        Id = Guid.NewGuid(),
                        ContributionRuleId = currentRule.Id,
                        HouseholdId = currentRule.HouseholdId,
                        HouseholdMemberId = currentRule.HouseholdMemberId,
                        TargetHouseholdAccountId = currentRule.TargetHouseholdAccountId,
                        PeriodKey = periodKey,
                        AmountMinor = currentRule.FixedAmountMinor.Value,
                        PaidAmountMinor = 0,
                        DueDateUtc = dueDateUtc,
                        StatusCode = HouseholdContributionStatuses.Pending,
                        ModeCode = HouseholdContributionModes.FixedAmount,
                        FixedAmountMinorSnapshot = currentRule.FixedAmountMinor,
                        PercentageBasisPointsSnapshot = null,
                        PlannedIncomeAmountMinor = null,
                        IncomeRuleId = null,
                        IncomeOccurrenceId = null,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<ActorHouseholdContext?> GetActorHouseholdAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var actorPersonId =
            await (
                from account in dbContext.UserAccounts.AsNoTracking()
                join person in dbContext.People.AsNoTracking()
                    on account.PersonId equals person.Id
                where
                    account.Id == actorUserId &&
                    account.IsActive &&
                    person.IsActive
                select (Guid?)person.Id)
                .SingleOrDefaultAsync(cancellationToken);

        if (!actorPersonId.HasValue)
        {
            return null;
        }

        var householdRows =
            await (
                from actorMembership in dbContext.HouseholdMembers.AsNoTracking()
                join activeHousehold in dbContext.Households.AsNoTracking()
                    on actorMembership.HouseholdId equals activeHousehold.Id
                where
                    actorMembership.PersonId == actorPersonId.Value &&
                    actorMembership.IsActive &&
                    activeHousehold.IsActive
                orderby actorMembership.JoinedAtUtc
                select activeHousehold)
                .Take(2)
                .ToArrayAsync(cancellationToken);

        if (householdRows.Length > 1)
        {
            throw new InvalidOperationException(
                "Użytkownik należy do więcej niż jednego aktywnego gospodarstwa.");
        }

        var household = householdRows.SingleOrDefault();

        return household is null
            ? null
            : new ActorHouseholdContext(
                actorPersonId.Value,
                household);
    }

    private static DateTime? ParsePeriodMonth(string periodKey)
    {
        if (periodKey.Length != 7 || periodKey[4] != '-')
        {
            return null;
        }

        if (!int.TryParse(periodKey[..4], out var year) ||
            !int.TryParse(periodKey[5..], out var month) ||
            year < 1 || month < 1 || month > 12)
        {
            return null;
        }

        return new DateTime(
            year,
            month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);
    }

    private static string GetPersonDisplayName(Person person) =>
        !string.IsNullOrWhiteSpace(person.DisplayName)
            ? person.DisplayName!
            : $"{person.FirstName} {person.LastName}".Trim();

    private static string NormalizeRequiredText(
        string? value,
        string fieldName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"{fieldName} nie może być puste.");
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"{fieldName} może mieć maksymalnie {maxLength} znaków.");
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(
        string? value,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"Wartość może mieć maksymalnie {maxLength} znaków.");
        }

        return normalized;
    }

    private sealed record ActorHouseholdContext(
        Guid PersonId,
        Household Household);
}
