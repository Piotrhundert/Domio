using Domio.Application.Auditing;
using Domio.Application.HouseholdFinance;
using Domio.Domain.HouseholdFinance;
using Domio.Domain.Users;
using Domio.Infrastructure.Authorization;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.Infrastructure.HouseholdFinance;

public sealed class HouseholdFinanceService(
    DomioDbContext dbContext,
    IAuditService auditService) : IHouseholdFinanceService
{
    public async Task<HouseholdFinanceOverview?> GetOverviewAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinanceHouseholdView,
            cancellationToken);

        var personId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        var household =
            await GetActiveHouseholdForPersonAsync(
                personId,
                cancellationToken);

        if (household is null)
        {
            return null;
        }

        var accounts =
            await dbContext.HouseholdAccounts
                .AsNoTracking()
                .Where(x =>
                    x.HouseholdId ==
                        household.Id)
                .OrderByDescending(x =>
                    x.IsActive)
                .ThenBy(x =>
                    x.Name)
                .ToArrayAsync(
                    cancellationToken);

        var balances =
            await dbContext.HouseholdEntries
                .AsNoTracking()
                .Where(x =>
                    x.HouseholdId ==
                        household.Id)
                .GroupBy(x =>
                    x.AccountId)
                .Select(x =>
                    new
                    {
                        AccountId =
                            x.Key,
                        BalanceMinor =
                            x.Sum(y =>
                                y.AmountMinor)
                    })
                .ToDictionaryAsync(
                    x => x.AccountId,
                    x => x.BalanceMinor,
                    cancellationToken);

        var accountSummaries =
            accounts
                .Select(account =>
                    new HouseholdAccountSummary(
                        account.Id,
                        account.Name,
                        account.AccountTypeCode,
                        HouseholdAccountTypes.GetNamePl(
                            account.AccountTypeCode),
                        account.CurrencyCode,
                        HouseholdFinanceMoney.FromMinorUnits(
                            balances.GetValueOrDefault(
                                account.Id)),
                        account.IsActive))
                .ToArray();

        var recentRows =
            await (
                from entry in dbContext.HouseholdEntries
                    .AsNoTracking()
                join account in dbContext.HouseholdAccounts
                    .AsNoTracking()
                    on entry.AccountId equals account.Id
                where
                    entry.HouseholdId ==
                        household.Id &&
                    account.HouseholdId ==
                        household.Id
                orderby
                    entry.OccurredAtUtc descending,
                    entry.CreatedAtUtc descending
                select new
                {
                    Entry = entry,
                    Account = account
                })
                .Take(30)
                .ToArrayAsync(
                    cancellationToken);

        var recentEntries =
            recentRows
                .Select(x =>
                    new HouseholdEntryItem(
                        x.Entry.Id,
                        x.Account.Id,
                        x.Account.Name,
                        x.Account.CurrencyCode,
                        x.Entry.EntryTypeCode,
                        HouseholdEntryTypes.GetNamePl(
                            x.Entry.EntryTypeCode),
                        HouseholdFinanceMoney.FromMinorUnits(
                            x.Entry.AmountMinor),
                        x.Entry.OccurredAtUtc,
                        x.Entry.CategoryCode,
                        HouseholdFinanceCategories.GetNamePl(
                            x.Entry.CategoryCode),
                        x.Entry.Description,
                        x.Entry.SourceType,
                        x.Entry.SourceId,
                        x.Entry.CorrectsEntryId))
                .ToArray();

        return new HouseholdFinanceOverview(
            household.Id,
            household.Name,
            household.CurrencyCode,
            accountSummaries,
            recentEntries);
    }

    public async Task<Guid> CreateAccountAsync(
        CreateHouseholdAccountRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinanceHouseholdManage,
            cancellationToken);

        var personId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        var name =
            NormalizeRequiredText(
                request.Name,
                "Nazwa konta",
                120);

        if (!HouseholdAccountTypes.IsValid(
                request.AccountTypeCode))
        {
            throw new ArgumentException(
                "Wybrano nieprawidłowy typ konta domu.");
        }

        var currencyCode =
            NormalizeCurrency(
                request.CurrencyCode);

        if (request.InitialBalance < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.InitialBalance),
                "Saldo początkowe nie może być ujemne.");
        }

        var initialBalanceMinor =
            HouseholdFinanceMoney.ToMinorUnits(
                request.InitialBalance);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var household =
            await GetActiveHouseholdForPersonAsync(
                personId,
                cancellationToken);

        var householdWasCreated =
            false;

        if (household is null)
        {
            var anyActiveHousehold =
                await dbContext.Households
                    .AsNoTracking()
                    .AnyAsync(
                        x => x.IsActive,
                        cancellationToken);

            if (anyActiveHousehold)
            {
                throw new UnauthorizedAccessException(
                    "Użytkownik nie jest przypisany do aktywnego gospodarstwa.");
            }

            var now =
                DateTime.UtcNow;

            household =
                new Household
                {
                    Id =
                        Guid.NewGuid(),
                    Name =
                        "Gospodarstwo domowe",
                    CurrencyCode =
                        currencyCode,
                    IsActive =
                        true,
                    CreatedByUserId =
                        actorUserId,
                    CreatedAtUtc =
                        now,
                    UpdatedAtUtc =
                        now
                };

            dbContext.Households.Add(
                household);

            dbContext.HouseholdMembers.Add(
                new HouseholdMember
                {
                    Id =
                        Guid.NewGuid(),
                    HouseholdId =
                        household.Id,
                    PersonId =
                        personId,
                    IsActive =
                        true,
                    JoinedAtUtc =
                        now
                });

            householdWasCreated =
                true;
        }

        var duplicateName =
            await dbContext.HouseholdAccounts
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.HouseholdId ==
                            household.Id &&
                        x.IsActive &&
                        x.Name.ToUpper() ==
                            name.ToUpper(),
                    cancellationToken);

        if (duplicateName)
        {
            throw new InvalidOperationException(
                "W tym gospodarstwie istnieje już aktywne konto o takiej nazwie.");
        }

        var createdAtUtc =
            DateTime.UtcNow;

        var account =
            new HouseholdAccount
            {
                Id =
                    Guid.NewGuid(),
                HouseholdId =
                    household.Id,
                Name =
                    name,
                AccountTypeCode =
                    request.AccountTypeCode,
                CurrencyCode =
                    currencyCode,
                IsActive =
                    true,
                CreatedAtUtc =
                    createdAtUtc,
                UpdatedAtUtc =
                    createdAtUtc
            };

        dbContext.HouseholdAccounts.Add(
            account);

        if (initialBalanceMinor > 0)
        {
            dbContext.HouseholdEntries.Add(
                new HouseholdEntry
                {
                    Id =
                        Guid.NewGuid(),
                    HouseholdId =
                        household.Id,
                    AccountId =
                        account.Id,
                    EntryTypeCode =
                        HouseholdEntryTypes.OpeningBalance,
                    AmountMinor =
                        initialBalanceMinor,
                    OccurredAtUtc =
                        createdAtUtc,
                    CategoryCode =
                        HouseholdFinanceCategories.HouseholdIncome,
                    Description =
                        "Saldo początkowe konta domu",
                    SourceType =
                        "AccountOpening",
                    SourceId =
                        account.Id.ToString(),
                    CreatedByUserId =
                        actorUserId,
                    CreatedAtUtc =
                        createdAtUtc
                });
        }

        await dbContext.SaveChangesAsync(
            cancellationToken);

        if (householdWasCreated)
        {
            await auditService.WriteAsync(
                new AuditEntry(
                    EventType:
                        "M04.1.HouseholdCreated",
                    EntityType:
                        "Household",
                    EntityId:
                        household.Id.ToString(),
                    ActorId:
                        actorUserId.ToString(),
                    CorrelationId:
                        correlationId,
                    Description:
                        "Utworzono pierwsze gospodarstwo dla modułu finansów domu."),
                cancellationToken);
        }

        await auditService.WriteAsync(
            new AuditEntry(
                EventType:
                    "M04.1.HouseholdAccountCreated",
                EntityType:
                    "HouseholdAccount",
                EntityId:
                    account.Id.ToString(),
                ActorId:
                    actorUserId.ToString(),
                CorrelationId:
                    correlationId,
                Description:
                    "Utworzono konto finansowe gospodarstwa. Nazwa konta i kwota początkowa nie są zapisywane w audycie."),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return account.Id;
    }

    public async Task<Guid> PostOperationAsync(
        PostHouseholdOperationRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinanceHouseholdManage,
            cancellationToken);

        var personId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        var household =
            await GetActiveHouseholdForPersonAsync(
                personId,
                cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Użytkownik nie jest przypisany do aktywnego gospodarstwa.");

        if (request.EntryTypeCode !=
                HouseholdEntryTypes.Income &&
            request.EntryTypeCode !=
                HouseholdEntryTypes.Expense)
        {
            throw new ArgumentException(
                "Ręczna operacja domu może być wpływem albo wydatkiem.");
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Amount),
                "Kwota operacji musi być większa od zera.");
        }

        var amountMinor =
            HouseholdFinanceMoney.ToMinorUnits(
                request.Amount);

        var categoryCode =
            string.IsNullOrWhiteSpace(
                request.CategoryCode)
                ? request.EntryTypeCode ==
                    HouseholdEntryTypes.Income
                    ? HouseholdFinanceCategories.HouseholdIncome
                    : HouseholdFinanceCategories.OtherExpense
                : request.CategoryCode.Trim();

        if (!HouseholdFinanceCategories.IsValid(
                categoryCode))
        {
            throw new ArgumentException(
                "Wybrano nieprawidłową kategorię finansów domu.");
        }

        var description =
            NormalizeOptionalText(
                request.Description,
                "Opis operacji",
                500);

        var sourceType =
            NormalizeOptionalText(
                request.SourceType,
                "Typ źródła",
                100)
            ?? "Manual";

        var sourceId =
            NormalizeOptionalText(
                request.SourceId,
                "Identyfikator źródła",
                200);

        var now =
            DateTime.UtcNow;

        var occurredAtUtc =
            NormalizeOccurredAtUtc(
                request.OccurredAtUtc,
                now);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var account =
            await dbContext.HouseholdAccounts
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            request.AccountId &&
                        x.HouseholdId ==
                            household.Id &&
                        x.IsActive,
                    cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Konto domu nie istnieje, jest nieaktywne albo należy do innego gospodarstwa.");

        var currentBalanceMinor =
            await dbContext.HouseholdEntries
                .Where(x =>
                    x.HouseholdId ==
                        household.Id &&
                    x.AccountId ==
                        account.Id)
                .SumAsync(
                    x => x.AmountMinor,
                    cancellationToken);

        long signedAmountMinor;

        if (request.EntryTypeCode ==
            HouseholdEntryTypes.Expense)
        {
            if (currentBalanceMinor <
                amountMinor)
            {
                throw new InvalidOperationException(
                    "Niewystarczające środki na koncie domu. Operacja została zablokowana.");
            }

            signedAmountMinor =
                -amountMinor;
        }
        else
        {
            signedAmountMinor =
                amountMinor;
        }

        var entry =
            new HouseholdEntry
            {
                Id =
                    Guid.NewGuid(),
                HouseholdId =
                    household.Id,
                AccountId =
                    account.Id,
                EntryTypeCode =
                    request.EntryTypeCode,
                AmountMinor =
                    signedAmountMinor,
                OccurredAtUtc =
                    occurredAtUtc,
                CategoryCode =
                    categoryCode,
                Description =
                    description,
                SourceType =
                    sourceType,
                SourceId =
                    sourceId,
                CreatedByUserId =
                    actorUserId,
                CreatedAtUtc =
                    now
            };

        dbContext.HouseholdEntries.Add(
            entry);

        account.UpdatedAtUtc =
            now;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType:
                    "M04.1.HouseholdEntryPosted",
                EntityType:
                    "HouseholdEntry",
                EntityId:
                    entry.Id.ToString(),
                ActorId:
                    actorUserId.ToString(),
                CorrelationId:
                    correlationId,
                Description:
                    "Zaksięgowano operację na koncie gospodarstwa. Kwota, nazwa konta i opis nie są zapisywane w audycie."),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return entry.Id;
    }

    public async Task<HouseholdTransferResult> TransferBetweenAccountsAsync(
        CreateHouseholdTransferRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinanceHouseholdManage,
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
            HouseholdFinanceMoney.ToMinorUnits(
                request.Amount);

        var description =
            NormalizeOptionalText(
                request.Description,
                "Opis transferu",
                500);

        var personId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        var household =
            await GetActiveHouseholdForPersonAsync(
                personId,
                cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Użytkownik nie jest przypisany do aktywnego gospodarstwa.");

        var now =
            DateTime.UtcNow;

        var occurredAtUtc =
            NormalizeOccurredAtUtc(
                request.OccurredAtUtc,
                now);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var accounts =
            await dbContext.HouseholdAccounts
                .Where(x =>
                    x.HouseholdId ==
                        household.Id &&
                    x.IsActive &&
                    (x.Id ==
                        request.SourceAccountId ||
                     x.Id ==
                        request.TargetAccountId))
                .ToArrayAsync(
                    cancellationToken);

        if (accounts.Length != 2)
        {
            throw new UnauthorizedAccessException(
                "Jedno z kont nie istnieje, jest nieaktywne albo należy do innego gospodarstwa.");
        }

        var source =
            accounts.Single(x =>
                x.Id ==
                    request.SourceAccountId);

        var target =
            accounts.Single(x =>
                x.Id ==
                    request.TargetAccountId);

        if (!string.Equals(
                source.CurrencyCode,
                target.CurrencyCode,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Transfer pomiędzy kontami domu w różnych walutach nie jest obsługiwany.");
        }

        var sourceBalanceMinor =
            await dbContext.HouseholdEntries
                .Where(x =>
                    x.HouseholdId ==
                        household.Id &&
                    x.AccountId ==
                        source.Id)
                .SumAsync(
                    x => x.AmountMinor,
                    cancellationToken);

        if (sourceBalanceMinor <
            amountMinor)
        {
            throw new InvalidOperationException(
                "Niewystarczające środki na koncie źródłowym. Transfer został zablokowany.");
        }

        var transferId =
            Guid.NewGuid();

        var sourceEntryId =
            Guid.NewGuid();

        var targetEntryId =
            Guid.NewGuid();

        dbContext.HouseholdEntries.AddRange(
            new HouseholdEntry
            {
                Id =
                    sourceEntryId,
                HouseholdId =
                    household.Id,
                AccountId =
                    source.Id,
                EntryTypeCode =
                    HouseholdEntryTypes.TransferOut,
                AmountMinor =
                    -amountMinor,
                OccurredAtUtc =
                    occurredAtUtc,
                CategoryCode =
                    null,
                Description =
                    string.IsNullOrWhiteSpace(description)
                        ? $"Transfer do: {target.Name}"
                        : $"Transfer do: {target.Name} · {description}",
                SourceType =
                    "HouseholdTransfer",
                SourceId =
                    transferId.ToString(),
                CreatedByUserId =
                    actorUserId,
                CreatedAtUtc =
                    now
            },
            new HouseholdEntry
            {
                Id =
                    targetEntryId,
                HouseholdId =
                    household.Id,
                AccountId =
                    target.Id,
                EntryTypeCode =
                    HouseholdEntryTypes.TransferIn,
                AmountMinor =
                    amountMinor,
                OccurredAtUtc =
                    occurredAtUtc,
                CategoryCode =
                    null,
                Description =
                    string.IsNullOrWhiteSpace(description)
                        ? $"Transfer z: {source.Name}"
                        : $"Transfer z: {source.Name} · {description}",
                SourceType =
                    "HouseholdTransfer",
                SourceId =
                    transferId.ToString(),
                CreatedByUserId =
                    actorUserId,
                CreatedAtUtc =
                    now
            });

        source.UpdatedAtUtc =
            now;

        target.UpdatedAtUtc =
            now;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType:
                    "M04.2.HouseholdTransferPosted",
                EntityType:
                    "HouseholdTransfer",
                EntityId:
                    transferId.ToString(),
                ActorId:
                    actorUserId.ToString(),
                CorrelationId:
                    correlationId,
                Description:
                    "Wykonano atomowy transfer pomiędzy kontami gospodarstwa. Kwota, nazwy kont i opis nie są zapisywane w audycie."),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return new HouseholdTransferResult(
            transferId,
            sourceEntryId,
            targetEntryId,
            source.Id,
            target.Id,
            request.Amount,
            source.CurrencyCode);
    }

    public async Task<HouseholdAccountClosureInfo?> GetAccountClosureInfoAsync(
        Guid accountId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinanceHouseholdManage,
            cancellationToken);

        var personId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        var household =
            await GetActiveHouseholdForPersonAsync(
                personId,
                cancellationToken);

        if (household is null)
        {
            return null;
        }

        var account =
            await dbContext.HouseholdAccounts
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            accountId &&
                        x.HouseholdId ==
                            household.Id,
                    cancellationToken);

        if (account is null)
        {
            return null;
        }

        var balanceMinor =
            await dbContext.HouseholdEntries
                .AsNoTracking()
                .Where(x =>
                    x.HouseholdId ==
                        household.Id &&
                    x.AccountId ==
                        account.Id)
                .SumAsync(
                    x => x.AmountMinor,
                    cancellationToken);

        return new HouseholdAccountClosureInfo(
            account.Id,
            account.Name,
            HouseholdAccountTypes.GetNamePl(
                account.AccountTypeCode),
            account.CurrencyCode,
            HouseholdFinanceMoney.FromMinorUnits(
                balanceMinor),
            account.IsActive);
    }

    public async Task CloseAccountAsync(
        Guid accountId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinanceHouseholdManage,
            cancellationToken);

        var personId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        var household =
            await GetActiveHouseholdForPersonAsync(
                personId,
                cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Użytkownik nie jest przypisany do aktywnego gospodarstwa.");

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var account =
            await dbContext.HouseholdAccounts
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            accountId &&
                        x.HouseholdId ==
                            household.Id,
                    cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Konto domu nie istnieje albo należy do innego gospodarstwa.");

        if (!account.IsActive)
        {
            return;
        }

        var balanceMinor =
            await dbContext.HouseholdEntries
                .Where(x =>
                    x.HouseholdId ==
                        household.Id &&
                    x.AccountId ==
                        account.Id)
                .SumAsync(
                    x => x.AmountMinor,
                    cancellationToken);

        if (balanceMinor != 0)
        {
            throw new InvalidOperationException(
                "Nie można zamknąć konta domu z niezerowym saldem. Najpierw przenieś lub rozlicz pozostałe środki.");
        }

        var now =
            DateTime.UtcNow;

        account.IsActive =
            false;

        account.ArchivedAtUtc =
            now;

        account.UpdatedAtUtc =
            now;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType:
                    "M04.2.HouseholdAccountClosed",
                EntityType:
                    "HouseholdAccount",
                EntityId:
                    account.Id.ToString(),
                ActorId:
                    actorUserId.ToString(),
                CorrelationId:
                    correlationId,
                Description:
                    "Zamknięto konto gospodarstwa z zerowym saldem. Historia księgowań pozostała zachowana."),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);
    }

    private async Task<Household?> GetActiveHouseholdForPersonAsync(
        Guid personId,
        CancellationToken cancellationToken)
    {
        var households =
            await (
                from membership in dbContext.HouseholdMembers
                    .AsNoTracking()
                join household in dbContext.Households
                    .AsNoTracking()
                    on membership.HouseholdId equals household.Id
                where
                    membership.PersonId ==
                        personId &&
                    membership.IsActive &&
                    household.IsActive
                orderby
                    membership.JoinedAtUtc
                select household)
                .Take(2)
                .ToArrayAsync(
                    cancellationToken);

        if (households.Length > 1)
        {
            throw new InvalidOperationException(
                "M04.2 obsługuje jedno aktywne gospodarstwo na użytkownika. Wybór wielu gospodarstw zostanie dodany w dalszym etapie.");
        }

        return households.SingleOrDefault();
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
                    account.Id ==
                        actorUserId &&
                    account.IsActive &&
                    person.IsActive
                select (Guid?)person.Id)
                .SingleOrDefaultAsync(
                    cancellationToken);

        return personId
            ?? throw new UnauthorizedAccessException(
                "Nie można ustalić aktywnej osoby powiązanej z kontem.");
    }

    private static string NormalizeCurrency(
        string? value)
    {
        var normalized =
            NormalizeRequiredText(
                value,
                "Waluta",
                3)
            .ToUpperInvariant();

        if (normalized.Length != 3 ||
            !normalized.All(char.IsLetter))
        {
            throw new ArgumentException(
                "Waluta musi mieć trzyliterowy kod, np. PLN.");
        }

        return normalized;
    }

    private static string NormalizeRequiredText(
        string? value,
        string fieldName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                $"{fieldName} nie może być pusta.");
        }

        var normalized =
            value.Trim();

        if (normalized.Length >
            maxLength)
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
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        var normalized =
            value.Trim();

        if (normalized.Length >
            maxLength)
        {
            throw new ArgumentException(
                $"{fieldName} może mieć maksymalnie {maxLength} znaków.");
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
            DateTimeKind.Utc =>
                value,
            DateTimeKind.Local =>
                value.ToUniversalTime(),
            _ =>
                DateTime.SpecifyKind(
                    value,
                    DateTimeKind.Utc)
        };
    }
}
