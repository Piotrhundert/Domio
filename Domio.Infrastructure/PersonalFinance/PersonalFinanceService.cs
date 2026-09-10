using Domio.Application.Auditing;
using Domio.Application.PersonalFinance;
using Domio.Domain.PersonalFinance;
using Domio.Domain.Users;
using Domio.Infrastructure.Authorization;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.Infrastructure.PersonalFinance;

public sealed class PersonalFinanceService(
    DomioDbContext dbContext,
    IAuditService auditService) : IPersonalFinanceService
{
    public async Task<PersonalFinanceOverview> GetOwnOverviewAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinancePersonalViewOwn,
            cancellationToken);

        var ownerPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        await EnsureRecurringOccurrencesAsync(
            ownerPersonId,
            DateTime.UtcNow.Date.AddMonths(12),
            cancellationToken);

        var accounts =
            await dbContext.PersonalFinancialAccounts
                .AsNoTracking()
                .Where(x =>
                    x.OwnerPersonId == ownerPersonId)
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => x.Name)
                .ToListAsync(cancellationToken);

        var accountIds =
            accounts.Select(x => x.Id).ToArray();

        var balancesByAccount =
            accountIds.Length == 0
                ? new Dictionary<Guid, long>()
                : await dbContext.PersonalFinancialTransactions
                    .AsNoTracking()
                    .Where(x =>
                        x.OwnerPersonId == ownerPersonId &&
                        accountIds.Contains(x.AccountId))
                    .GroupBy(x => x.AccountId)
                    .Select(group => new
                    {
                        AccountId = group.Key,
                        BalanceMinor =
                            group.Sum(x => x.AmountMinor)
                    })
                    .ToDictionaryAsync(
                        x => x.AccountId,
                        x => x.BalanceMinor,
                        cancellationToken);

        var summaries =
            accounts
                .Select(account =>
                    new PersonalAccountSummary(
                        account.Id,
                        account.Name,
                        account.AccountTypeCode,
                        PersonalAccountTypes.GetNamePl(
                            account.AccountTypeCode),
                        account.CurrencyCode,
                        PersonalFinanceMoney.FromMinorUnits(
                            balancesByAccount.GetValueOrDefault(
                                account.Id)),
                        account.IsActive))
                .ToArray();

        var recentTransactions =
            accountIds.Length == 0
                ? Array.Empty<PersonalTransactionItem>()
                : await dbContext.PersonalFinancialTransactions
                    .AsNoTracking()
                    .Where(x =>
                        x.OwnerPersonId == ownerPersonId &&
                        accountIds.Contains(x.AccountId))
                    .OrderByDescending(x => x.OccurredAtUtc)
                    .ThenByDescending(x => x.CreatedAtUtc)
                    .Take(30)
                    .Select(x =>
                        new PersonalTransactionItem(
                            x.Id,
                            x.AccountId,
                            x.KindCode,
                            PersonalTransactionKinds.GetNamePl(
                                x.KindCode),
                            PersonalFinanceMoney.FromMinorUnits(
                                x.AmountMinor),
                            x.OccurredAtUtc,
                            x.Description,
                            x.CorrectsTransactionId,
                            x.CreatedAtUtc))
                    .ToArrayAsync(cancellationToken);

        var recurringRules =
            await (
                from rule in dbContext.PersonalRecurringRules
                    .AsNoTracking()
                join account in dbContext.PersonalFinancialAccounts
                    .AsNoTracking()
                    on rule.AccountId equals account.Id
                where rule.OwnerPersonId == ownerPersonId
                orderby rule.IsActive descending, rule.Name
                select new PersonalRecurringRuleSummary(
                    rule.Id,
                    rule.AccountId,
                    account.Name,
                    rule.KindCode,
                    PersonalTransactionKinds.GetNamePl(rule.KindCode),
                    rule.Name,
                    PersonalFinanceMoney.FromMinorUnits(
                        rule.PlannedAmountMinor),
                    rule.CurrencyCode,
                    rule.FrequencyCode,
                    PersonalRecurringFrequencies.GetNamePl(
                        rule.FrequencyCode),
                    rule.CategoryCode,
                    PersonalFinanceCategories.GetNamePl(
                        rule.CategoryCode),
                    rule.Counterparty,
                    rule.StartDateUtc,
                    rule.EndDateUtc,
                    rule.IsActive))
                .ToArrayAsync(cancellationToken);

        var fromDate =
            DateTime.UtcNow.Date.AddMonths(-1);
        var throughDate =
            DateTime.UtcNow.Date.AddMonths(6);

        var recurringOccurrences =
            await (
                from occurrence in dbContext.PersonalRecurringOccurrences
                    .AsNoTracking()
                join rule in dbContext.PersonalRecurringRules
                    .AsNoTracking()
                    on occurrence.RecurringRuleId equals rule.Id
                join account in dbContext.PersonalFinancialAccounts
                    .AsNoTracking()
                    on rule.AccountId equals account.Id
                where occurrence.OwnerPersonId == ownerPersonId &&
                      occurrence.PlannedDateUtc >= fromDate &&
                      occurrence.PlannedDateUtc <= throughDate
                orderby occurrence.PlannedDateUtc, rule.Name
                select new PersonalRecurringOccurrenceItem(
                    occurrence.Id,
                    rule.Id,
                    occurrence.AccountId ?? rule.AccountId,
                    occurrence.AccountName ?? account.Name,
                    occurrence.RuleName ?? rule.Name,
                    occurrence.KindCode ?? rule.KindCode,
                    PersonalTransactionKinds.GetNamePl(
                        occurrence.KindCode ?? rule.KindCode),
                    PersonalFinanceCategories.GetNamePl(
                        occurrence.CategoryCode ?? rule.CategoryCode),
                    occurrence.Counterparty ?? rule.Counterparty,
                    occurrence.PeriodKey,
                    occurrence.PlannedDateUtc,
                    PersonalFinanceMoney.FromMinorUnits(
                        occurrence.PlannedAmountMinor),
                    occurrence.CurrencyCode,
                    occurrence.StatusCode,
                    PersonalRecurringOccurrenceStatuses.GetNamePl(
                        occurrence.StatusCode),
                    occurrence.ActualTransactionId,
                    occurrence.ActualAmountMinor.HasValue
                        ? PersonalFinanceMoney.FromMinorUnits(
                            occurrence.ActualAmountMinor.Value)
                        : null,
                    occurrence.ActualDateUtc))
                .ToArrayAsync(cancellationToken);

        return new PersonalFinanceOverview(
            ownerPersonId,
            summaries,
            recentTransactions,
            recurringRules,
            recurringOccurrences);
    }

    public async Task<Guid> CreateOwnAccountAsync(
        CreatePersonalAccountRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinancePersonalManageOwn,
            cancellationToken);

        var ownerPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        var name =
            NormalizeRequiredText(
                request.Name,
                "Nazwa konta",
                120);

        if (!PersonalAccountTypes.IsValid(
                request.AccountTypeCode))
        {
            throw new ArgumentException(
                "Wybrano nieprawidłowy typ konta.");
        }

        var currencyCode =
            NormalizeCurrencyCode(
                request.CurrencyCode);

        var initialBalanceMinor =
            PersonalFinanceMoney.ToMinorUnits(
                request.InitialBalance);

        var normalizedName =
            name.ToUpperInvariant();

        var duplicateExists =
            await dbContext.PersonalFinancialAccounts
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.OwnerPersonId == ownerPersonId &&
                        x.IsActive &&
                        x.Name.ToUpper() ==
                            normalizedName,
                    cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException(
                "Masz już aktywne konto o takiej nazwie.");
        }

        var now = DateTime.UtcNow;

        var account =
            new PersonalFinancialAccount
            {
                Id = Guid.NewGuid(),
                OwnerPersonId = ownerPersonId,
                Name = name,
                AccountTypeCode =
                    request.AccountTypeCode,
                CurrencyCode = currencyCode,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        dbContext.PersonalFinancialAccounts.Add(
            account);

        if (initialBalanceMinor != 0)
        {
            dbContext.PersonalFinancialTransactions.Add(
                new PersonalFinancialTransaction
                {
                    Id = Guid.NewGuid(),
                    AccountId = account.Id,
                    OwnerPersonId = ownerPersonId,
                    KindCode =
                        PersonalTransactionKinds.OpeningBalance,
                    AmountMinor = initialBalanceMinor,
                    OccurredAtUtc = now,
                    Description = "Saldo początkowe",
                    CreatedByUserId = actorUserId,
                    CreatedAtUtc = now
                });
        }

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M03.1.PersonalAccountCreated",
                EntityType: "PersonalFinancialAccount",
                EntityId: account.Id.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Użytkownik utworzył własne prywatne konto finansowe. Nazwa konta i kwoty nie są zapisywane w audycie."),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return account.Id;
    }

    public async Task<Guid> PostOwnOperationAsync(
        PostPersonalOperationRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinancePersonalManageOwn,
            cancellationToken);

        var ownerPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        if (request.KindCode !=
                PersonalTransactionKinds.Income &&
            request.KindCode !=
                PersonalTransactionKinds.Expense)
        {
            throw new ArgumentException(
                "W M03.1 ręcznie można dodać tylko przychód albo wydatek.");
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Amount),
                "Kwota operacji musi być większa od zera.");
        }

        var amountMinor =
            PersonalFinanceMoney.ToMinorUnits(
                request.Amount);

        var description =
            NormalizeOptionalText(
                request.Description,
                "Opis operacji",
                500);

        var account =
            await dbContext.PersonalFinancialAccounts
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == request.AccountId &&
                        x.OwnerPersonId ==
                            ownerPersonId &&
                        x.IsActive,
                    cancellationToken);

        if (account is null)
        {
            throw new UnauthorizedAccessException(
                "Konto nie istnieje albo nie należy do zalogowanego użytkownika.");
        }

        var currentBalanceMinor =
            await dbContext.PersonalFinancialTransactions
                .Where(x =>
                    x.AccountId == account.Id &&
                    x.OwnerPersonId ==
                        ownerPersonId)
                .SumAsync(
                    x => x.AmountMinor,
                    cancellationToken);

        long signedAmountMinor;

        if (request.KindCode ==
            PersonalTransactionKinds.Expense)
        {
            if (currentBalanceMinor <
                amountMinor)
            {
                throw new InvalidOperationException(
                    "Niewystarczające środki na koncie. Operacja została zablokowana.");
            }

            signedAmountMinor =
                -amountMinor;
        }
        else
        {
            signedAmountMinor =
                amountMinor;
        }

        var now = DateTime.UtcNow;

        var financialTransaction =
            new PersonalFinancialTransaction
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                OwnerPersonId = ownerPersonId,
                KindCode = request.KindCode,
                AmountMinor = signedAmountMinor,
                OccurredAtUtc =
                    NormalizeOccurredAtUtc(
                        request.OccurredAtUtc,
                        now),
                Description = description,
                CreatedByUserId = actorUserId,
                CreatedAtUtc = now
            };

        await using var dbTransaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        dbContext.PersonalFinancialTransactions.Add(
            financialTransaction);

        account.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M03.1.PersonalTransactionPosted",
                EntityType: "PersonalFinancialTransaction",
                EntityId:
                    financialTransaction.Id.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Użytkownik zarejestrował operację na własnym prywatnym koncie. Kwota i opis nie są zapisywane w audycie."),
            cancellationToken);

        await dbTransaction.CommitAsync(
            cancellationToken);

        return financialTransaction.Id;
    }

    public async Task<Guid> CreateOwnRecurringRuleAsync(
        CreatePersonalRecurringRuleRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinancePersonalManageOwn,
            cancellationToken);

        var ownerPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        if (request.KindCode != PersonalTransactionKinds.Income &&
            request.KindCode != PersonalTransactionKinds.Expense)
        {
            throw new ArgumentException(
                "Reguła cykliczna może dotyczyć przychodu albo wydatku.");
        }

        var name =
            NormalizeRequiredText(
                request.Name,
                "Nazwa reguły",
                160);

        if (request.PlannedAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.PlannedAmount),
                "Planowana kwota musi być większa od zera.");
        }

        var plannedAmountMinor =
            PersonalFinanceMoney.ToMinorUnits(
                request.PlannedAmount);

        if (!PersonalRecurringFrequencies.IsValid(
                request.FrequencyCode))
        {
            throw new ArgumentException(
                "Wybrano nieprawidłową częstotliwość.");
        }

        if (!PersonalFinanceCategories.IsValid(
                request.CategoryCode))
        {
            throw new ArgumentException(
                "Wybrano nieprawidłową kategorię.");
        }

        var counterparty =
            NormalizeOptionalText(
                request.Counterparty,
                "Firma / instytucja / usługodawca",
                200);

        if (request.KindCode == PersonalTransactionKinds.Income &&
            string.IsNullOrWhiteSpace(counterparty))
        {
            throw new ArgumentException(
                "Dla cyklicznego przychodu podaj firmę lub instytucję płatnika.");
        }

        var startDate =
            DateTime.SpecifyKind(
                request.StartDateUtc.Date,
                DateTimeKind.Utc);

        DateTime? endDate =
            request.EndDateUtc.HasValue
                ? DateTime.SpecifyKind(
                    request.EndDateUtc.Value.Date,
                    DateTimeKind.Utc)
                : null;

        if (endDate.HasValue &&
            endDate.Value < startDate)
        {
            throw new ArgumentException(
                "Data końca nie może być wcześniejsza od daty początku.");
        }

        var account =
            await dbContext.PersonalFinancialAccounts
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == request.AccountId &&
                        x.OwnerPersonId == ownerPersonId &&
                        x.IsActive,
                    cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Konto nie istnieje albo nie należy do zalogowanego użytkownika.");

        var now = DateTime.UtcNow;

        var rule =
            new PersonalRecurringRule
            {
                Id = Guid.NewGuid(),
                OwnerPersonId = ownerPersonId,
                AccountId = account.Id,
                KindCode = request.KindCode,
                Name = name,
                PlannedAmountMinor = plannedAmountMinor,
                CurrencyCode = account.CurrencyCode,
                FrequencyCode = request.FrequencyCode,
                CategoryCode = request.CategoryCode,
                Counterparty = counterparty,
                StartDateUtc = startDate,
                EndDateUtc = endDate,
                IsActive = true,
                CreatedByUserId = actorUserId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        dbContext.PersonalRecurringRules.Add(rule);

        await dbContext.SaveChangesAsync(cancellationToken);

        await EnsureOccurrencesForRuleAsync(
            rule,
            DateTime.UtcNow.Date.AddMonths(12),
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M03.3.PersonalRecurringRuleCreated",
                EntityType: "PersonalRecurringRule",
                EntityId: rule.Id.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Użytkownik utworzył prywatną regułę cyklicznego przychodu lub wydatku. Kwota, nazwa i płatnik nie są zapisywane w audycie."),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return rule.Id;
    }

    public async Task<PersonalRecurringRuleSummary?> GetOwnRecurringRuleAsync(
        Guid ruleId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinancePersonalViewOwn,
            cancellationToken);

        var ownerPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        return await (
            from rule in dbContext.PersonalRecurringRules
                .AsNoTracking()
            join account in dbContext.PersonalFinancialAccounts
                .AsNoTracking()
                on rule.AccountId equals account.Id
            where
                rule.Id == ruleId &&
                rule.OwnerPersonId == ownerPersonId
            select new PersonalRecurringRuleSummary(
                rule.Id,
                rule.AccountId,
                account.Name,
                rule.KindCode,
                PersonalTransactionKinds.GetNamePl(
                    rule.KindCode),
                rule.Name,
                PersonalFinanceMoney.FromMinorUnits(
                    rule.PlannedAmountMinor),
                rule.CurrencyCode,
                rule.FrequencyCode,
                PersonalRecurringFrequencies.GetNamePl(
                    rule.FrequencyCode),
                rule.CategoryCode,
                PersonalFinanceCategories.GetNamePl(
                    rule.CategoryCode),
                rule.Counterparty,
                rule.StartDateUtc,
                rule.EndDateUtc,
                rule.IsActive))
            .SingleOrDefaultAsync(
                cancellationToken);
    }

    public async Task UpdateOwnRecurringRuleAsync(
        UpdatePersonalRecurringRuleRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinancePersonalManageOwn,
            cancellationToken);

        var ownerPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        var rule =
            await dbContext.PersonalRecurringRules
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == request.RuleId &&
                        x.OwnerPersonId ==
                            ownerPersonId,
                    cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Reguła nie istnieje albo nie należy do zalogowanego użytkownika.");

        if (!rule.IsActive)
        {
            throw new InvalidOperationException(
                "Nie można edytować zakończonej reguły cyklicznej.");
        }

        ValidateRecurringRuleInput(
            request.KindCode,
            request.Name,
            request.PlannedAmount,
            request.FrequencyCode,
            request.CategoryCode,
            request.Counterparty,
            request.StartDateUtc,
            request.EndDateUtc,
            out var normalizedName,
            out var plannedAmountMinor,
            out var normalizedCounterparty,
            out var startDate,
            out var endDate);

        var account =
            await dbContext.PersonalFinancialAccounts
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == request.AccountId &&
                        x.OwnerPersonId ==
                            ownerPersonId &&
                        x.IsActive,
                    cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Konto nie istnieje albo nie należy do zalogowanego użytkownika.");

        var now =
            DateTime.UtcNow;

        var today =
            DateTime.UtcNow.Date;

        // Przyszły plan można przeliczyć, bo nie jest jeszcze księgowaniem.
        // Pozycje z terminem wcześniejszym niż dziś pozostają bez zmian,
        // aby np. wynagrodzenie planowane na 01.09 nadal można było potwierdzić.
        var futurePlannedOccurrences =
            await dbContext.PersonalRecurringOccurrences
                .Where(x =>
                    x.RecurringRuleId == rule.Id &&
                    x.StatusCode ==
                        PersonalRecurringOccurrenceStatuses.Planned &&
                    x.PlannedDateUtc >= today)
                .ToListAsync(
                    cancellationToken);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        dbContext.PersonalRecurringOccurrences.RemoveRange(
            futurePlannedOccurrences);

        rule.AccountId =
            account.Id;
        rule.KindCode =
            request.KindCode;
        rule.Name =
            normalizedName;
        rule.PlannedAmountMinor =
            plannedAmountMinor;
        rule.CurrencyCode =
            account.CurrencyCode;
        rule.FrequencyCode =
            request.FrequencyCode;
        rule.CategoryCode =
            request.CategoryCode;
        rule.Counterparty =
            normalizedCounterparty;
        rule.StartDateUtc =
            startDate;
        rule.EndDateUtc =
            endDate;
        rule.UpdatedAtUtc =
            now;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await EnsureOccurrencesForRuleAsync(
            rule,
            DateTime.UtcNow.Date.AddMonths(12),
            cancellationToken,
            today);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType:
                    "M03.4.PersonalRecurringRuleUpdated",
                EntityType:
                    "PersonalRecurringRule",
                EntityId:
                    rule.Id.ToString(),
                ActorId:
                    actorUserId.ToString(),
                CorrelationId:
                    correlationId,
                Description:
                    "Użytkownik zmienił prywatną regułę cykliczną. Zmiana dotyczy przyszłego planu; wcześniejsze wystąpienia pozostają zachowane. Kwoty i nazwy nie są zapisywane w audycie."),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);
    }

    public async Task DeactivateOwnRecurringRuleAsync(
        Guid ruleId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinancePersonalManageOwn,
            cancellationToken);

        var ownerPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        var rule =
            await dbContext.PersonalRecurringRules
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == ruleId &&
                        x.OwnerPersonId ==
                            ownerPersonId,
                    cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Reguła nie istnieje albo nie należy do zalogowanego użytkownika.");

        if (!rule.IsActive)
        {
            return;
        }

        var today =
            DateTime.UtcNow.Date;

        var futurePlannedOccurrences =
            await dbContext.PersonalRecurringOccurrences
                .Where(x =>
                    x.RecurringRuleId == rule.Id &&
                    x.StatusCode ==
                        PersonalRecurringOccurrenceStatuses.Planned &&
                    x.PlannedDateUtc >= today)
                .ToListAsync(
                    cancellationToken);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        dbContext.PersonalRecurringOccurrences.RemoveRange(
            futurePlannedOccurrences);

        rule.IsActive =
            false;
        rule.UpdatedAtUtc =
            DateTime.UtcNow;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType:
                    "M03.4.PersonalRecurringRuleDeactivated",
                EntityType:
                    "PersonalRecurringRule",
                EntityId:
                    rule.Id.ToString(),
                ActorId:
                    actorUserId.ToString(),
                CorrelationId:
                    correlationId,
                Description:
                    "Użytkownik zakończył prywatną regułę cykliczną. Przyszły niezaksięgowany plan został usunięty, a wcześniejsza historia zachowana."),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);
    }

    public async Task<PersonalRecurringOccurrenceItem?> GetOwnRecurringOccurrenceAsync(
        Guid occurrenceId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinancePersonalViewOwn,
            cancellationToken);

        var ownerPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        return await (
            from occurrence in dbContext.PersonalRecurringOccurrences
                .AsNoTracking()
            join rule in dbContext.PersonalRecurringRules
                .AsNoTracking()
                on occurrence.RecurringRuleId equals rule.Id
            join account in dbContext.PersonalFinancialAccounts
                .AsNoTracking()
                on rule.AccountId equals account.Id
            where
                occurrence.Id == occurrenceId &&
                occurrence.OwnerPersonId ==
                    ownerPersonId &&
                rule.OwnerPersonId ==
                    ownerPersonId
            select new PersonalRecurringOccurrenceItem(
                occurrence.Id,
                rule.Id,
                occurrence.AccountId ?? rule.AccountId,
                occurrence.AccountName ?? account.Name,
                occurrence.RuleName ?? rule.Name,
                occurrence.KindCode ?? rule.KindCode,
                PersonalTransactionKinds.GetNamePl(
                    occurrence.KindCode ?? rule.KindCode),
                PersonalFinanceCategories.GetNamePl(
                    occurrence.CategoryCode ?? rule.CategoryCode),
                occurrence.Counterparty ?? rule.Counterparty,
                occurrence.PeriodKey,
                occurrence.PlannedDateUtc,
                PersonalFinanceMoney.FromMinorUnits(
                    occurrence.PlannedAmountMinor),
                occurrence.CurrencyCode,
                occurrence.StatusCode,
                PersonalRecurringOccurrenceStatuses.GetNamePl(
                    occurrence.StatusCode),
                occurrence.ActualTransactionId,
                occurrence.ActualAmountMinor.HasValue
                    ? PersonalFinanceMoney.FromMinorUnits(
                        occurrence.ActualAmountMinor.Value)
                    : null,
                occurrence.ActualDateUtc))
            .SingleOrDefaultAsync(
                cancellationToken);
    }

    public async Task<Guid> ConfirmOwnRecurringOccurrenceAsync(
        ConfirmPersonalRecurringOccurrenceRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinancePersonalManageOwn,
            cancellationToken);

        var ownerPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        if (request.ActualAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.ActualAmount),
                "Rzeczywista kwota musi być większa od zera.");
        }

        var actualAmountMinor =
            PersonalFinanceMoney.ToMinorUnits(
                request.ActualAmount);

        var description =
            NormalizeOptionalText(
                request.Description,
                "Opis operacji",
                500);

        var occurrence =
            await dbContext.PersonalRecurringOccurrences
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == request.OccurrenceId &&
                        x.OwnerPersonId ==
                            ownerPersonId,
                    cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Planowane wystąpienie nie istnieje albo nie należy do zalogowanego użytkownika.");

        if (occurrence.StatusCode !=
            PersonalRecurringOccurrenceStatuses.Planned)
        {
            throw new InvalidOperationException(
                "To wystąpienie zostało już rozliczone.");
        }

        var rule =
            await dbContext.PersonalRecurringRules
                .SingleAsync(
                    x =>
                        x.Id ==
                            occurrence.RecurringRuleId &&
                        x.OwnerPersonId ==
                            ownerPersonId,
                    cancellationToken);

        var occurrenceAccountId =
            occurrence.AccountId ??
            rule.AccountId;

        var occurrenceKindCode =
            occurrence.KindCode ??
            rule.KindCode;

        var account =
            await dbContext.PersonalFinancialAccounts
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == occurrenceAccountId &&
                        x.OwnerPersonId ==
                            ownerPersonId &&
                        x.IsActive,
                    cancellationToken)
            ?? throw new InvalidOperationException(
                "Konto przypisane do planowanego wystąpienia nie jest aktywne.");

        var currentBalanceMinor =
            await dbContext.PersonalFinancialTransactions
                .Where(x =>
                    x.AccountId ==
                        account.Id &&
                    x.OwnerPersonId ==
                        ownerPersonId)
                .SumAsync(
                    x => x.AmountMinor,
                    cancellationToken);

        long signedAmountMinor;

        if (occurrenceKindCode ==
            PersonalTransactionKinds.Expense)
        {
            if (currentBalanceMinor <
                actualAmountMinor)
            {
                throw new InvalidOperationException(
                    "Niewystarczające środki na koncie. Potwierdzenie wydatku zostało zablokowane.");
            }

            signedAmountMinor =
                -actualAmountMinor;
        }
        else
        {
            signedAmountMinor =
                actualAmountMinor;
        }

        var now =
            DateTime.UtcNow;

        var actualDate =
            NormalizeOccurredAtUtc(
                request.ActualDateUtc,
                now);

        var financialTransaction =
            new PersonalFinancialTransaction
            {
                Id = Guid.NewGuid(),
                AccountId =
                    account.Id,
                OwnerPersonId =
                    ownerPersonId,
                KindCode =
                    occurrenceKindCode,
                AmountMinor =
                    signedAmountMinor,
                OccurredAtUtc =
                    actualDate,
                Description =
                    description ??
                    occurrence.RuleName ??
                    rule.Name,
                CreatedByUserId =
                    actorUserId,
                CreatedAtUtc =
                    now
            };

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        dbContext.PersonalFinancialTransactions.Add(
            financialTransaction);

        occurrence.StatusCode =
            PersonalRecurringOccurrenceStatuses.Confirmed;
        occurrence.ActualTransactionId =
            financialTransaction.Id;
        occurrence.ActualAmountMinor =
            actualAmountMinor;
        occurrence.ActualDateUtc =
            actualDate;
        occurrence.UpdatedAtUtc =
            now;

        account.UpdatedAtUtc =
            now;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType:
                    "M03.4.PersonalRecurringOccurrenceConfirmed",
                EntityType:
                    "PersonalRecurringOccurrence",
                EntityId:
                    occurrence.Id.ToString(),
                ActorId:
                    actorUserId.ToString(),
                CorrelationId:
                    correlationId,
                Description:
                    "Użytkownik potwierdził rzeczywisty wpływ lub płatność wynikającą z prywatnej reguły cyklicznej. Planowana kwota i data pozostają zachowane."),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return financialTransaction.Id;
    }

    public async Task<PersonalTransferResult> TransferBetweenOwnAccountsAsync(
        CreatePersonalTransferRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinancePersonalManageOwn,
            cancellationToken);

        var ownerPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        if (request.SourceAccountId ==
            request.TargetAccountId)
        {
            throw new ArgumentException(
                "Konto źródłowe i docelowe muszą być różne.");
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Amount),
                "Kwota transferu musi być większa od zera.");
        }

        var amountMinor =
            PersonalFinanceMoney.ToMinorUnits(
                request.Amount);

        var description =
            NormalizeOptionalText(
                request.Description,
                "Opis transferu",
                300);

        var now =
            DateTime.UtcNow;

        var occurredAtUtc =
            NormalizeOccurredAtUtc(
                request.OccurredAtUtc,
                now);

        await using var dbTransaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var accounts =
            await dbContext.PersonalFinancialAccounts
                .Where(x =>
                    x.OwnerPersonId == ownerPersonId &&
                    x.IsActive &&
                    (x.Id == request.SourceAccountId ||
                     x.Id == request.TargetAccountId))
                .ToListAsync(
                    cancellationToken);

        if (accounts.Count != 2)
        {
            throw new UnauthorizedAccessException(
                "Jedno z kont nie istnieje, jest nieaktywne albo nie należy do zalogowanego użytkownika.");
        }

        var sourceAccount =
            accounts.Single(x =>
                x.Id ==
                request.SourceAccountId);

        var targetAccount =
            accounts.Single(x =>
                x.Id ==
                request.TargetAccountId);

        if (!string.Equals(
                sourceAccount.CurrencyCode,
                targetAccount.CurrencyCode,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Transfer między kontami w różnych walutach nie jest jeszcze obsługiwany.");
        }

        var sourceBalanceMinor =
            await dbContext.PersonalFinancialTransactions
                .Where(x =>
                    x.AccountId ==
                        sourceAccount.Id &&
                    x.OwnerPersonId ==
                        ownerPersonId)
                .SumAsync(
                    x => x.AmountMinor,
                    cancellationToken);

        if (sourceBalanceMinor <
            amountMinor)
        {
            throw new InvalidOperationException(
                "Niewystarczające środki na koncie źródłowym. Transfer został zablokowany.");
        }

        var sourceTransactionId =
            Guid.NewGuid();

        var targetTransactionId =
            Guid.NewGuid();

        var sourceDescription =
            string.IsNullOrWhiteSpace(description)
                ? $"Transfer do: {targetAccount.Name}"
                : $"Transfer do: {targetAccount.Name} · {description}";

        var targetDescription =
            string.IsNullOrWhiteSpace(description)
                ? $"Transfer z: {sourceAccount.Name}"
                : $"Transfer z: {sourceAccount.Name} · {description}";

        dbContext.PersonalFinancialTransactions.AddRange(
            new PersonalFinancialTransaction
            {
                Id =
                    sourceTransactionId,
                AccountId =
                    sourceAccount.Id,
                OwnerPersonId =
                    ownerPersonId,
                KindCode =
                    PersonalTransactionKinds.TransferOut,
                AmountMinor =
                    -amountMinor,
                OccurredAtUtc =
                    occurredAtUtc,
                Description =
                    sourceDescription,
                CreatedByUserId =
                    actorUserId,
                CreatedAtUtc =
                    now
            },
            new PersonalFinancialTransaction
            {
                Id =
                    targetTransactionId,
                AccountId =
                    targetAccount.Id,
                OwnerPersonId =
                    ownerPersonId,
                KindCode =
                    PersonalTransactionKinds.TransferIn,
                AmountMinor =
                    amountMinor,
                OccurredAtUtc =
                    occurredAtUtc,
                Description =
                    targetDescription,
                CreatedByUserId =
                    actorUserId,
                CreatedAtUtc =
                    now
            });

        sourceAccount.UpdatedAtUtc =
            now;

        targetAccount.UpdatedAtUtc =
            now;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType:
                    "M03.5.PersonalAccountTransferPosted",
                EntityType:
                    "PersonalFinancialTransaction",
                EntityId:
                    sourceTransactionId.ToString(),
                ActorId:
                    actorUserId.ToString(),
                CorrelationId:
                    correlationId,
                Description:
                    "Użytkownik wykonał atomowy transfer pomiędzy dwoma własnymi prywatnymi kontami. Kwota, nazwy kont i opis nie są zapisywane w audycie."),
            cancellationToken);

        await dbTransaction.CommitAsync(
            cancellationToken);

        return new PersonalTransferResult(
            sourceTransactionId,
            targetTransactionId,
            sourceAccount.Id,
            targetAccount.Id,
            PersonalFinanceMoney.FromMinorUnits(
                amountMinor),
            sourceAccount.CurrencyCode);
    }

    private static void ValidateRecurringRuleInput(
        string kindCode,
        string name,
        decimal plannedAmount,
        string frequencyCode,
        string categoryCode,
        string? counterparty,
        DateTime startDateUtc,
        DateTime? endDateUtc,
        out string normalizedName,
        out long plannedAmountMinor,
        out string? normalizedCounterparty,
        out DateTime startDate,
        out DateTime? endDate)
    {
        if (kindCode !=
                PersonalTransactionKinds.Income &&
            kindCode !=
                PersonalTransactionKinds.Expense)
        {
            throw new ArgumentException(
                "Reguła cykliczna może dotyczyć przychodu albo wydatku.");
        }

        normalizedName =
            NormalizeRequiredText(
                name,
                "Nazwa reguły",
                160);

        if (plannedAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(plannedAmount),
                "Planowana kwota musi być większa od zera.");
        }

        plannedAmountMinor =
            PersonalFinanceMoney.ToMinorUnits(
                plannedAmount);

        if (!PersonalRecurringFrequencies.IsValid(
                frequencyCode))
        {
            throw new ArgumentException(
                "Wybrano nieprawidłową częstotliwość.");
        }

        if (!PersonalFinanceCategories.IsValid(
                categoryCode))
        {
            throw new ArgumentException(
                "Wybrano nieprawidłową kategorię.");
        }

        normalizedCounterparty =
            NormalizeOptionalText(
                counterparty,
                "Firma / instytucja / usługodawca",
                200);

        if (kindCode ==
                PersonalTransactionKinds.Income &&
            string.IsNullOrWhiteSpace(
                normalizedCounterparty))
        {
            throw new ArgumentException(
                "Dla cyklicznego przychodu podaj firmę lub instytucję płatnika.");
        }

        startDate =
            DateTime.SpecifyKind(
                startDateUtc.Date,
                DateTimeKind.Utc);

        endDate =
            endDateUtc.HasValue
                ? DateTime.SpecifyKind(
                    endDateUtc.Value.Date,
                    DateTimeKind.Utc)
                : null;

        if (endDate.HasValue &&
            endDate.Value < startDate)
        {
            throw new ArgumentException(
                "Data końca nie może być wcześniejsza od daty początku.");
        }
    }

    private async Task EnsureRecurringOccurrencesAsync(
        Guid ownerPersonId,
        DateTime throughDateUtc,
        CancellationToken cancellationToken)
    {
        var rules =
            await dbContext.PersonalRecurringRules
                .Where(x =>
                    x.OwnerPersonId == ownerPersonId &&
                    x.IsActive)
                .ToListAsync(cancellationToken);

        foreach (var rule in rules)
        {
            await EnsureOccurrencesForRuleAsync(
                rule,
                throughDateUtc,
                cancellationToken);
        }
    }

    private async Task EnsureOccurrencesForRuleAsync(
        PersonalRecurringRule rule,
        DateTime throughDateUtc,
        CancellationToken cancellationToken,
        DateTime? notBeforeDateUtc = null)
    {
        var endDate =
            rule.EndDateUtc.HasValue &&
            rule.EndDateUtc.Value.Date < throughDateUtc.Date
                ? rule.EndDateUtc.Value.Date
                : throughDateUtc.Date;

        var existingPeriodKeys =
            await dbContext.PersonalRecurringOccurrences
                .AsNoTracking()
                .Where(x =>
                    x.RecurringRuleId == rule.Id)
                .Select(x => x.PeriodKey)
                .ToHashSetAsync(cancellationToken);

        var date = rule.StartDateUtc.Date;
        var generated = 0;
        var now = DateTime.UtcNow;

        if (notBeforeDateUtc.HasValue)
        {
            var floorDate =
                notBeforeDateUtc.Value.Date;

            while (date < floorDate)
            {
                date =
                    GetNextRecurringDate(
                        rule,
                        date);

                generated++;

                if (generated > 240)
                {
                    throw new InvalidOperationException(
                        "Reguła cykliczna przekroczyła bezpieczny limit wyznaczania dat.");
                }
            }
        }

        while (date <= endDate)
        {
            generated++;

            if (generated > 240)
            {
                throw new InvalidOperationException(
                    "Reguła cykliczna przekroczyła bezpieczny limit generowania wystąpień.");
            }

            var periodKey =
                rule.FrequencyCode switch
                {
                    PersonalRecurringFrequencies.Monthly =>
                        date.ToString("yyyy-MM"),
                    PersonalRecurringFrequencies.Quarterly =>
                        $"{date:yyyy}-Q{((date.Month - 1) / 3) + 1}",
                    PersonalRecurringFrequencies.Yearly =>
                        date.ToString("yyyy"),
                    _ => throw new InvalidOperationException(
                        "Nieobsługiwana częstotliwość.")
                };

            if (!existingPeriodKeys.Contains(periodKey))
            {
                dbContext.PersonalRecurringOccurrences.Add(
                    new PersonalRecurringOccurrence
                    {
                        Id = Guid.NewGuid(),
                        RecurringRuleId = rule.Id,
                        OwnerPersonId = rule.OwnerPersonId,
                        AccountId = rule.AccountId,
                        AccountName =
                            dbContext.PersonalFinancialAccounts
                                .Where(x => x.Id == rule.AccountId)
                                .Select(x => x.Name)
                                .Single(),
                        KindCode = rule.KindCode,
                        RuleName = rule.Name,
                        CategoryCode = rule.CategoryCode,
                        Counterparty = rule.Counterparty,
                        PeriodKey = periodKey,
                        PlannedDateUtc = date,
                        PlannedAmountMinor =
                            rule.PlannedAmountMinor,
                        CurrencyCode = rule.CurrencyCode,
                        StatusCode =
                            PersonalRecurringOccurrenceStatuses.Planned,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    });

                existingPeriodKeys.Add(periodKey);
            }

            date =
                GetNextRecurringDate(
                    rule,
                    date);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static DateTime GetNextRecurringDate(
        PersonalRecurringRule rule,
        DateTime currentDate)
    {
        var monthsToAdd =
            rule.FrequencyCode switch
            {
                PersonalRecurringFrequencies.Monthly => 1,
                PersonalRecurringFrequencies.Quarterly => 3,
                PersonalRecurringFrequencies.Yearly => 12,
                _ => throw new InvalidOperationException(
                    "Nieobsługiwana częstotliwość reguły.")
            };

        var nextMonth =
            new DateTime(
                currentDate.Year,
                currentDate.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc)
            .AddMonths(monthsToAdd);

        var day =
            Math.Min(
                rule.StartDateUtc.Day,
                DateTime.DaysInMonth(
                    nextMonth.Year,
                    nextMonth.Month));

        return new DateTime(
            nextMonth.Year,
            nextMonth.Month,
            day,
            0,
            0,
            0,
            DateTimeKind.Utc);
    }

    private async Task<Guid> GetActorPersonIdAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var personId =
            await (
                from account in dbContext.UserAccounts
                    .AsNoTracking()
                join person in dbContext.People
                    .AsNoTracking()
                    on account.PersonId equals person.Id
                where
                    account.Id == actorUserId &&
                    account.IsActive &&
                    person.IsActive
                select (Guid?)person.Id)
                .SingleOrDefaultAsync(cancellationToken);

        return personId
            ?? throw new UnauthorizedAccessException(
                "Nie można ustalić aktywnej osoby powiązanej z kontem.");
    }

    private static string NormalizeRequiredText(
        string? value,
        string fieldName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"{fieldName} nie może być pusta.");
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
        string fieldName,
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
                $"{fieldName} może mieć maksymalnie {maxLength} znaków.");
        }

        return normalized;
    }

    private static string NormalizeCurrencyCode(
        string? currencyCode)
    {
        var normalized =
            string.IsNullOrWhiteSpace(currencyCode)
                ? "PLN"
                : currencyCode.Trim().ToUpperInvariant();

        if (normalized.Length != 3 ||
            normalized.Any(x =>
                !char.IsLetter(x)))
        {
            throw new ArgumentException(
                "Kod waluty musi składać się z trzech liter, np. PLN.");
        }

        return normalized;
    }

    private static DateTime NormalizeOccurredAtUtc(
        DateTime value,
        DateTime fallbackUtc)
    {
        if (value == default)
        {
            return fallbackUtc;
        }

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(
                value,
                DateTimeKind.Utc)
        };
    }
}
