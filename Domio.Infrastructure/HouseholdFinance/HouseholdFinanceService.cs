using Domio.Application.Auditing;
using Domio.Application.HouseholdFinance;
using Domio.Domain.HouseholdFinance;
using Domio.Domain.PersonalFinance;
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

    public async Task<HouseholdContributionOverview?> GetContributionOverviewAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinanceHouseholdView,
            cancellationToken);

        var actorPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        var household =
            await GetActiveHouseholdForPersonAsync(
                actorPersonId,
                cancellationToken);

        if (household is null)
        {
            return null;
        }

        var canManage =
            await PermissionEnforcement.HasUserAsync(
                dbContext,
                actorUserId,
                SystemPermissions.FinanceHouseholdManage,
                cancellationToken);

        var canApprove =
            await PermissionEnforcement.HasUserAsync(
                dbContext,
                actorUserId,
                SystemPermissions.FinanceHouseholdApprove,
                cancellationToken);

        HouseholdContributionRoleOption[] roleOptions = [];

        if (canManage)
        {
            var eligibleRoleIds =
                new[]
                {
                    SystemRoles.AdministratorId,
                    SystemRoles.HouseholdMemberId
                };

            var roleDefinitions =
                await dbContext.RoleDefinitions
                    .AsNoTracking()
                    .Where(x =>
                        eligibleRoleIds.Contains(
                            x.Id))
                    .OrderBy(x =>
                        x.Id)
                    .ToArrayAsync(
                        cancellationToken);

            var activeRoleCounts =
                await (
                    from account in dbContext.UserAccounts
                        .AsNoTracking()
                    join person in dbContext.People
                        .AsNoTracking()
                        on account.PersonId equals person.Id
                    where
                        account.IsActive &&
                        person.IsActive &&
                        eligibleRoleIds.Contains(
                            account.RoleDefinitionId)
                    group account by account.RoleDefinitionId
                    into roleGroup
                    select new
                    {
                        RoleDefinitionId =
                            roleGroup.Key,
                        Count =
                            roleGroup.Count()
                    })
                    .ToDictionaryAsync(
                        x => x.RoleDefinitionId,
                        x => x.Count,
                        cancellationToken);

            roleOptions =
                roleDefinitions
                    .Where(x =>
                        activeRoleCounts.GetValueOrDefault(
                            x.Id) > 0)
                    .Select(x =>
                        new HouseholdContributionRoleOption(
                            x.Id,
                            x.Code,
                            x.NamePl,
                            activeRoleCounts.GetValueOrDefault(
                                x.Id)))
                    .ToArray();

        }

        var memberRows =
            await (
                from membership in dbContext.HouseholdMembers
                    .AsNoTracking()
                join person in dbContext.People
                    .AsNoTracking()
                    on membership.PersonId equals person.Id
                where
                    membership.HouseholdId ==
                        household.Id &&
                    membership.IsActive &&
                    person.IsActive
                orderby
                    person.LastName,
                    person.FirstName
                select new
                {
                    Membership = membership,
                    Person = person
                })
                .ToArrayAsync(
                    cancellationToken);

        var actorMembership =
            memberRows
                .SingleOrDefault(x =>
                    x.Person.Id ==
                        actorPersonId);

        if (!canManage &&
            !canApprove &&
            actorMembership is null)
        {
            return null;
        }

        await GenerateContributionObligationsAsync(
            household.Id,
            DateTime.UtcNow.Date.AddMonths(12),
            cancellationToken);

        var canSeeAllContributions =
            canManage ||
            canApprove;

        Guid? actorMembershipId =
            actorMembership?.Membership.Id;

        var visibleMemberIds =
            canSeeAllContributions
                ? memberRows
                    .Select(x =>
                        x.Membership.Id)
                    .ToArray()
                : [actorMembership!.Membership.Id];

        var members =
            memberRows
                .Where(x =>
                    visibleMemberIds.Contains(
                        x.Membership.Id))
                .Select(x =>
                    new HouseholdMemberOption(
                        x.Membership.Id,
                        x.Person.Id,
                        GetPersonDisplayName(
                            x.Person)))
                .ToArray();

        var targetAccounts =
            await dbContext.HouseholdAccounts
                .AsNoTracking()
                .Where(x =>
                    x.HouseholdId ==
                        household.Id &&
                    x.IsActive)
                .OrderBy(x =>
                    x.Name)
                .ToArrayAsync(
                    cancellationToken);

        var accountBalances =
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
                        AccountId = x.Key,
                        BalanceMinor =
                            x.Sum(y =>
                                y.AmountMinor)
                    })
                .ToDictionaryAsync(
                    x => x.AccountId,
                    x => x.BalanceMinor,
                    cancellationToken);

        var targetAccountSummaries =
            targetAccounts
                .Select(x =>
                    new HouseholdAccountSummary(
                        x.Id,
                        x.Name,
                        x.AccountTypeCode,
                        HouseholdAccountTypes.GetNamePl(
                            x.AccountTypeCode),
                        x.CurrencyCode,
                        HouseholdFinanceMoney.FromMinorUnits(
                            accountBalances.GetValueOrDefault(
                                x.Id)),
                        x.IsActive))
                .ToArray();

        HouseholdMemberCandidate[] memberCandidates = [];

        if (canManage)
        {
            var activeMemberPersonIds =
                memberRows
                    .Select(x =>
                        x.Person.Id)
                    .ToArray();

            memberCandidates =
                await (
                    from account in dbContext.UserAccounts
                        .AsNoTracking()
                    join person in dbContext.People
                        .AsNoTracking()
                        on account.PersonId equals person.Id
                    join role in dbContext.RoleDefinitions
                        .AsNoTracking()
                        on account.RoleDefinitionId equals role.Id
                    where
                        account.IsActive &&
                        person.IsActive &&
                        role.Code ==
                            SystemRoles.HouseholdMemberCode &&
                        !activeMemberPersonIds.Contains(
                            person.Id)
                    orderby
                        person.LastName,
                        person.FirstName
                    select new HouseholdMemberCandidate(
                        person.Id,
                        (
                            (person.DisplayName ?? string.Empty) != string.Empty
                                ? person.DisplayName!
                                : person.FirstName + " " + person.LastName
                        ),
                        account.LoginName))
                    .ToArrayAsync(
                        cancellationToken);
        }

        HouseholdContributionIncomeRuleOption[] incomeRules = [];

        if (canManage)
        {
            var allMemberMap =
                memberRows.ToDictionary(
                    x => x.Person.Id,
                    x => x.Membership.Id);

            var memberPersonIds =
                allMemberMap.Keys.ToArray();

            var incomeRuleRows =
                await dbContext.PersonalRecurringRules
                    .AsNoTracking()
                    .Where(x =>
                        memberPersonIds.Contains(
                            x.OwnerPersonId) &&
                        x.IsActive &&
                        x.KindCode ==
                            PersonalTransactionKinds.Income &&
                        x.FrequencyCode ==
                            PersonalRecurringFrequencies.Monthly)
                    .OrderBy(x =>
                        x.Name)
                    .ToArrayAsync(
                        cancellationToken);

            incomeRules =
                incomeRuleRows
                    .Select(x =>
                        new HouseholdContributionIncomeRuleOption(
                            x.Id,
                            allMemberMap[x.OwnerPersonId],
                            x.Name,
                            PersonalFinanceMoney.FromMinorUnits(
                                x.PlannedAmountMinor),
                            x.CurrencyCode,
                            x.StartDateUtc.Day))
                    .ToArray();
        }

        var ruleRows =
            await (
                from rule in dbContext.HouseholdContributionRules
                    .AsNoTracking()
                join membership in dbContext.HouseholdMembers
                    .AsNoTracking()
                    on rule.HouseholdMemberId equals membership.Id
                join person in dbContext.People
                    .AsNoTracking()
                    on membership.PersonId equals person.Id
                join targetAccount in dbContext.HouseholdAccounts
                    .AsNoTracking()
                    on rule.TargetHouseholdAccountId equals targetAccount.Id
                join incomeRule in dbContext.PersonalRecurringRules
                    .AsNoTracking()
                    on rule.IncomeRuleId equals (Guid?)incomeRule.Id
                    into incomeRuleJoin
                from incomeRule in incomeRuleJoin.DefaultIfEmpty()
                where
                    rule.HouseholdId ==
                        household.Id &&
                    visibleMemberIds.Contains(
                        rule.HouseholdMemberId)
                orderby
                    rule.IsActive descending,
                    person.LastName,
                    person.FirstName,
                    rule.CreatedAtUtc descending
                select new
                {
                    Rule = rule,
                    Person = person,
                    TargetAccount = targetAccount,
                    IncomeRule = incomeRule
                })
                .ToArrayAsync(
                    cancellationToken);

        var ruleSummaries =
            ruleRows
                .Select(x =>
                    new HouseholdContributionRuleSummary(
                        x.Rule.Id,
                        x.Rule.HouseholdMemberId,
                        GetPersonDisplayName(
                            x.Person),
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
                        x.Rule.IncomeRuleId,
                        x.IncomeRule?.Name,
                        x.IncomeRule is not null
                            ? PersonalFinanceMoney.FromMinorUnits(
                                x.IncomeRule.PlannedAmountMinor)
                            : null,
                        x.IncomeRule?.StartDateUtc.Day,
                        x.Rule.DueOffsetDays,
                        x.Rule.TargetHouseholdAccountId,
                        x.TargetAccount.Name,
                        x.Rule.ValidFromUtc,
                        x.Rule.ValidToUtc,
                        x.Rule.ReminderDays,
                        x.Rule.IsActive))
                .ToArray();

        var obligationRows =
            await (
                from obligation in dbContext.HouseholdContributionObligations
                    .AsNoTracking()
                join contributionRule in dbContext.HouseholdContributionRules
                    .AsNoTracking()
                    on obligation.ContributionRuleId equals contributionRule.Id
                join membership in dbContext.HouseholdMembers
                    .AsNoTracking()
                    on obligation.HouseholdMemberId equals membership.Id
                join person in dbContext.People
                    .AsNoTracking()
                    on membership.PersonId equals person.Id
                join targetAccount in dbContext.HouseholdAccounts
                    .AsNoTracking()
                    on obligation.TargetHouseholdAccountId equals targetAccount.Id
                where
                    obligation.HouseholdId ==
                        household.Id &&
                    contributionRule.HouseholdId ==
                        household.Id &&
                    visibleMemberIds.Contains(
                        obligation.HouseholdMemberId)
                orderby
                    obligation.DueDateUtc,
                    person.LastName,
                    person.FirstName
                select new
                {
                    Obligation = obligation,
                    ContributionRule = contributionRule,
                    Person = person,
                    TargetAccount = targetAccount
                })
                .Take(100)
                .ToArrayAsync(
                    cancellationToken);

        var todayUtc =
            DateTime.UtcNow.Date;

        var obligations =
            obligationRows
                .Select(x =>
                {
                    var effectiveStatus =
                        ResolveContributionStatus(
                            x.Obligation,
                            todayUtc);

                    var outstandingMinor =
                        Math.Max(
                            0,
                            x.Obligation.AmountMinor -
                            x.Obligation.PaidAmountMinor);

                    var reminder =
                        HouseholdContributionReminderStates.Resolve(
                            x.Obligation.DueDateUtc,
                            x.ContributionRule.ReminderDays,
                            outstandingMinor,
                            effectiveStatus,
                            todayUtc);

                    return new HouseholdContributionObligationItem(
                        x.Obligation.Id,
                        x.Obligation.ContributionRuleId,
                        x.Obligation.HouseholdMemberId,
                        GetPersonDisplayName(
                            x.Person),
                        x.Obligation.PeriodKey,
                        HouseholdFinanceMoney.FromMinorUnits(
                            x.Obligation.AmountMinor),
                        HouseholdFinanceMoney.FromMinorUnits(
                            x.Obligation.PaidAmountMinor),
                        HouseholdFinanceMoney.FromMinorUnits(
                            outstandingMinor),
                        x.Obligation.DueDateUtc,
                        effectiveStatus,
                        HouseholdContributionStatuses.GetNamePl(
                            effectiveStatus),
                        x.Obligation.ModeCode,
                        x.Obligation.PercentageBasisPointsSnapshot.HasValue
                            ? HouseholdContributionMath.FromBasisPoints(
                                x.Obligation.PercentageBasisPointsSnapshot.Value)
                            : null,
                        x.Obligation.PlannedIncomeAmountMinor.HasValue
                            ? HouseholdFinanceMoney.FromMinorUnits(
                                x.Obligation.PlannedIncomeAmountMinor.Value)
                            : null,
                        x.Obligation.TargetHouseholdAccountId,
                        x.TargetAccount.Name,
                        x.TargetAccount.CurrencyCode,
                        actorMembership is not null &&
                        x.Obligation.HouseholdMemberId ==
                            actorMembership.Membership.Id,
                        x.ContributionRule.ReminderDays,
                        reminder.Code,
                        reminder.NamePl,
                        reminder.DaysToDue,
                        reminder.IsActive);
                })
                .ToArray();

        var paymentRequestRows =
            await (
                from paymentRequest in dbContext.HouseholdContributionPaymentRequests
                    .AsNoTracking()
                join obligation in dbContext.HouseholdContributionObligations
                    .AsNoTracking()
                    on paymentRequest.ObligationId equals obligation.Id
                join membership in dbContext.HouseholdMembers
                    .AsNoTracking()
                    on paymentRequest.HouseholdMemberId equals membership.Id
                join person in dbContext.People
                    .AsNoTracking()
                    on membership.PersonId equals person.Id
                join targetAccount in dbContext.HouseholdAccounts
                    .AsNoTracking()
                    on paymentRequest.TargetHouseholdAccountId equals targetAccount.Id
                join sourceAccount in dbContext.PersonalFinancialAccounts
                    .AsNoTracking()
                    on paymentRequest.SourcePersonalAccountId equals sourceAccount.Id
                where
                    paymentRequest.HouseholdId ==
                        household.Id &&
                    (
                        canApprove ||
                        (
                            actorMembershipId.HasValue &&
                            paymentRequest.HouseholdMemberId ==
                                actorMembershipId.Value
                        )
                    )
                orderby
                    paymentRequest.StatusCode ==
                        HouseholdContributionPaymentStatuses.Pending
                        descending,
                    paymentRequest.SubmittedAtUtc descending
                select new
                {
                    PaymentRequest = paymentRequest,
                    Obligation = obligation,
                    Person = person,
                    TargetAccount = targetAccount,
                    SourceAccount = sourceAccount
                })
                .Take(100)
                .ToArrayAsync(
                    cancellationToken);

        var paymentRequests =
            paymentRequestRows
                .Select(x =>
                {
                    var isOwn =
                        actorMembershipId.HasValue &&
                        x.PaymentRequest.HouseholdMemberId ==
                            actorMembershipId.Value;

                    return new HouseholdContributionPaymentRequestItem(
                        x.PaymentRequest.Id,
                        x.PaymentRequest.ObligationId,
                        x.PaymentRequest.HouseholdMemberId,
                        GetPersonDisplayName(
                            x.Person),
                        x.Obligation.PeriodKey,
                        HouseholdFinanceMoney.FromMinorUnits(
                            x.PaymentRequest.AmountMinor),
                        x.TargetAccount.CurrencyCode,
                        x.PaymentRequest.StatusCode,
                        HouseholdContributionPaymentStatuses.GetNamePl(
                            x.PaymentRequest.StatusCode),
                        x.PaymentRequest.SubmittedAtUtc,
                        x.PaymentRequest.ReviewedAtUtc,
                        x.PaymentRequest.ReviewNote,
                        isOwn,
                        isOwn
                            ? x.SourceAccount.Name
                            : null);
                })
                .ToArray();

        return new HouseholdContributionOverview(
            household.Id,
            household.Name,
            household.CurrencyCode,
            canManage,
            canApprove,
            roleOptions,
            members,
            memberCandidates,
            incomeRules,
            targetAccountSummaries,
            ruleSummaries,
            obligations,
            paymentRequests);
    }

    public async Task<Guid> AddHouseholdMemberAsync(
        AddHouseholdMemberRequest request,
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

        var actorPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        var household =
            await GetActiveHouseholdForPersonAsync(
                actorPersonId,
                cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Użytkownik nie jest przypisany do aktywnego gospodarstwa.");

        var candidate =
            await (
                from account in dbContext.UserAccounts
                join person in dbContext.People
                    on account.PersonId equals person.Id
                join role in dbContext.RoleDefinitions
                    on account.RoleDefinitionId equals role.Id
                where
                    person.Id ==
                        request.PersonId &&
                    account.IsActive &&
                    person.IsActive &&
                    role.Code ==
                        SystemRoles.HouseholdMemberCode
                select new
                {
                    Account = account,
                    Person = person
                })
                .SingleOrDefaultAsync(
                    cancellationToken)
            ?? throw new ArgumentException(
                "Wybrana osoba nie jest aktywnym użytkownikiem z rolą Domownik.");

        var existing =
            await dbContext.HouseholdMembers
                .SingleOrDefaultAsync(
                    x =>
                        x.HouseholdId ==
                            household.Id &&
                        x.PersonId ==
                            candidate.Person.Id,
                    cancellationToken);

        if (existing is not null &&
            existing.IsActive)
        {
            return existing.Id;
        }

        var now =
            DateTime.UtcNow;

        Guid householdMemberId;

        if (existing is null)
        {
            var membership =
                new HouseholdMember
                {
                    Id =
                        Guid.NewGuid(),
                    HouseholdId =
                        household.Id,
                    PersonId =
                        candidate.Person.Id,
                    IsActive =
                        true,
                    JoinedAtUtc =
                        now
                };

            dbContext.HouseholdMembers.Add(
                membership);

            householdMemberId =
                membership.Id;
        }
        else
        {
            existing.IsActive =
                true;
            existing.JoinedAtUtc =
                now;
            existing.LeftAtUtc =
                null;

            householdMemberId =
                existing.Id;
        }

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType:
                    "M04.3.HouseholdMemberAdded",
                EntityType:
                    "HouseholdMember",
                EntityId:
                    householdMemberId.ToString(),
                ActorId:
                    actorUserId.ToString(),
                CorrelationId:
                    correlationId,
                Description:
                    "Dodano aktywnego użytkownika z rolą Domownik do gospodarstwa. Dane osobowe nie są zapisywane w opisie audytu."),
            cancellationToken);

        return householdMemberId;
    }

    public async Task<HouseholdContributionBatchResult> CreateContributionRuleAsync(
        CreateHouseholdContributionRuleRequest request,
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

        var actorPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        var household =
            await GetActiveHouseholdForPersonAsync(
                actorPersonId,
                cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Użytkownik nie jest przypisany do aktywnego gospodarstwa.");

        if (request.RoleDefinitionId !=
                SystemRoles.AdministratorId &&
            request.RoleDefinitionId !=
                SystemRoles.HouseholdMemberId)
        {
            throw new ArgumentException(
                "Składkę zbiorczą można obecnie ustawić dla roli Administrator albo Domownik.");
        }

        var role =
            await dbContext.RoleDefinitions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            request.RoleDefinitionId,
                    cancellationToken)
            ?? throw new ArgumentException(
                "Wybrana rola nie istnieje.");

        if (!HouseholdContributionModes.IsValid(
                request.ModeCode))
        {
            throw new ArgumentException(
                "Wybrano nieprawidłowy tryb składki.");
        }

        if (request.DueOffsetDays < 0 ||
            request.DueOffsetDays > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.DueOffsetDays),
                "Termin składki musi mieścić się w zakresie 0-31 dni od planowanej wypłaty.");
        }

        if (request.ReminderDays < 0 ||
            request.ReminderDays > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.ReminderDays),
                "Przypomnienie musi mieścić się w zakresie 0-31 dni.");
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

        if (validToUtc.HasValue &&
            validToUtc.Value <
                validFromUtc)
        {
            throw new ArgumentException(
                "Data końcowa reguły nie może być wcześniejsza od daty początkowej.");
        }

        long? fixedAmountMinor =
            null;

        int? percentageBasisPoints =
            null;

        if (request.ModeCode ==
            HouseholdContributionModes.FixedAmount)
        {
            if (!request.FixedAmount.HasValue ||
                request.FixedAmount.Value <= 0)
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

        var targetAccount =
            await dbContext.HouseholdAccounts
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            request.TargetHouseholdAccountId &&
                        x.HouseholdId ==
                            household.Id &&
                        x.IsActive,
                    cancellationToken)
            ?? throw new ArgumentException(
                "Wybrane konto docelowe gospodarstwa nie istnieje lub jest nieaktywne.");

        var selectedUsers =
            await (
                from account in dbContext.UserAccounts
                    .AsNoTracking()
                join person in dbContext.People
                    .AsNoTracking()
                    on account.PersonId equals person.Id
                where
                    account.RoleDefinitionId ==
                        request.RoleDefinitionId &&
                    account.IsActive &&
                    person.IsActive
                orderby
                    person.LastName,
                    person.FirstName
                select new
                {
                    account.Id,
                    account.PersonId,
                    Person = person
                })
                .ToArrayAsync(
                    cancellationToken);

        if (selectedUsers.Length == 0)
        {
            throw new InvalidOperationException(
                "Brak aktywnych użytkowników z wybraną rolą.");
        }

        var selectedPersonIds =
            selectedUsers
                .Select(x =>
                    x.PersonId)
                .ToArray();

        var salaryRules =
            await dbContext.PersonalRecurringRules
                .AsNoTracking()
                .Where(x =>
                    selectedPersonIds.Contains(
                        x.OwnerPersonId) &&
                    x.IsActive &&
                    x.KindCode ==
                        PersonalTransactionKinds.Income &&
                    x.FrequencyCode ==
                        PersonalRecurringFrequencies.Monthly &&
                    x.CategoryCode ==
                        PersonalFinanceCategories.Salary)
                .OrderBy(x =>
                    x.CreatedAtUtc)
                .ToArrayAsync(
                    cancellationToken);

        var salaryRulesByPerson =
            salaryRules
                .GroupBy(x =>
                    x.OwnerPersonId)
                .ToDictionary(
                    x => x.Key,
                    x => x.ToArray());

        foreach (var user in selectedUsers)
        {
            if (!salaryRulesByPerson.TryGetValue(
                    user.PersonId,
                    out var userSalaryRules) ||
                userSalaryRules.Length != 1)
            {
                // Brak wynagrodzenia albo kilka aktywnych planów nie blokuje
                // utworzenia składki dla roli. Reguła zostanie zapisana
                // jako oczekująca i podpięta automatycznie, gdy pojawi się
                // dokładnie jedno jednoznaczne planowane wynagrodzenie.
                continue;
            }

            var salaryRule =
                userSalaryRules[0];

            if (!string.Equals(
                    salaryRule.CurrencyCode,
                    targetAccount.CurrencyCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Waluta planowanego wynagrodzenia użytkownika {GetPersonDisplayName(user.Person)} nie jest zgodna z walutą konta docelowego gospodarstwa.");
            }
        }

        var now =
            DateTime.UtcNow;

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var existingMemberships =
            await dbContext.HouseholdMembers
                .Where(x =>
                    x.HouseholdId ==
                        household.Id &&
                    selectedPersonIds.Contains(
                        x.PersonId))
                .ToArrayAsync(
                    cancellationToken);

        var membershipByPerson =
            existingMemberships.ToDictionary(
                x => x.PersonId);

        foreach (var user in selectedUsers)
        {
            if (membershipByPerson.TryGetValue(
                    user.PersonId,
                    out var existingMembership))
            {
                if (!existingMembership.IsActive)
                {
                    existingMembership.IsActive =
                        true;
                    existingMembership.JoinedAtUtc =
                        now;
                    existingMembership.LeftAtUtc =
                        null;
                }

                continue;
            }

            var membership =
                new HouseholdMember
                {
                    Id =
                        Guid.NewGuid(),
                    HouseholdId =
                        household.Id,
                    PersonId =
                        user.PersonId,
                    IsActive =
                        true,
                    JoinedAtUtc =
                        now
                };

            dbContext.HouseholdMembers.Add(
                membership);

            membershipByPerson[user.PersonId] =
                membership;
        }

        await dbContext.SaveChangesAsync(
            cancellationToken);

        var activeMemberIds =
            membershipByPerson.Values
                .Where(x =>
                    x.IsActive)
                .Select(x =>
                    x.Id)
                .ToArray();

        var existingActiveRules =
            await dbContext.HouseholdContributionRules
                .AsNoTracking()
                .Where(x =>
                    x.HouseholdId ==
                        household.Id &&
                    x.IsActive &&
                    activeMemberIds.Contains(
                        x.HouseholdMemberId))
                .ToArrayAsync(
                    cancellationToken);

        if (existingActiveRules.Length > 0)
        {
            var conflictingMemberIds =
                existingActiveRules
                    .Select(x =>
                        x.HouseholdMemberId)
                    .Distinct()
                    .ToHashSet();

            var conflictingNames =
                selectedUsers
                    .Where(x =>
                        conflictingMemberIds.Contains(
                            membershipByPerson[x.PersonId].Id))
                    .Select(x =>
                        GetPersonDisplayName(
                            x.Person))
                    .ToArray();

            throw new InvalidOperationException(
                "Co najmniej jedna osoba z wybranej roli ma już aktywną regułę składki. Najpierw zakończ lub zmień istniejącą regułę. Dotyczy: " +
                string.Join(
                    ", ",
                    conflictingNames));
        }

        var createdRules =
            new List<HouseholdContributionRule>(
                selectedUsers.Length);

        foreach (var user in selectedUsers)
        {
            var membership =
                membershipByPerson[user.PersonId];

            PersonalRecurringRule? salaryRule =
                null;

            if (salaryRulesByPerson.TryGetValue(
                    user.PersonId,
                    out var userSalaryRules) &&
                userSalaryRules.Length == 1)
            {
                salaryRule =
                    userSalaryRules[0];
            }

            var rule =
                new HouseholdContributionRule
                {
                    Id =
                        Guid.NewGuid(),
                    HouseholdId =
                        household.Id,
                    HouseholdMemberId =
                        membership.Id,
                    ModeCode =
                        request.ModeCode,
                    FixedAmountMinor =
                        fixedAmountMinor,
                    PercentageBasisPoints =
                        percentageBasisPoints,
                    IncomeRuleId =
                        salaryRule?.Id,
                    DueOffsetDays =
                        request.DueOffsetDays,
                    TargetHouseholdAccountId =
                        targetAccount.Id,
                    ValidFromUtc =
                        validFromUtc,
                    ValidToUtc =
                        validToUtc,
                    ReminderDays =
                        request.ReminderDays,
                    IsActive =
                        true,
                    CreatedByUserId =
                        actorUserId,
                    CreatedAtUtc =
                        now,
                    UpdatedAtUtc =
                        now
                };

            dbContext.HouseholdContributionRules.Add(
                rule);

            createdRules.Add(
                rule);
        }

        await dbContext.SaveChangesAsync(
            cancellationToken);

        foreach (var ruleToGenerate in createdRules)
        {
            await GenerateContributionObligationsForRuleAsync(
                ruleToGenerate,
                DateTime.UtcNow.Date.AddMonths(12),
                cancellationToken);
        }

        await auditService.WriteAsync(
            new AuditEntry(
                EventType:
                    "M04.3.HouseholdContributionRoleBatchCreated",
                EntityType:
                    "HouseholdContributionRuleBatch",
                EntityId:
                    Guid.NewGuid().ToString(),
                ActorId:
                    actorUserId.ToString(),
                CorrelationId:
                    correlationId,
                Description:
                    $"Utworzono zbiorczą konfigurację składki dla roli {role.NamePl}. Liczba utworzonych reguł: {createdRules.Count}. Kwoty, procenty i dane wynagrodzeń nie są zapisywane w audycie."),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return new HouseholdContributionBatchResult(
            role.Id,
            role.NamePl,
            selectedUsers.Length,
            createdRules.Count,
            createdRules.Count(x =>
                !x.IncomeRuleId.HasValue),
            createdRules
                .Select(x =>
                    x.Id)
                .ToArray());
    }

    public async Task<HouseholdContributionPaymentForm?> GetContributionPaymentFormAsync(
        Guid obligationId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinanceHouseholdView,
            cancellationToken);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinancePersonalManageOwn,
            cancellationToken);

        var actorPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        var household =
            await GetActiveHouseholdForPersonAsync(
                actorPersonId,
                cancellationToken);

        if (household is null)
        {
            return null;
        }

        await GenerateContributionObligationsAsync(
            household.Id,
            DateTime.UtcNow.Date.AddMonths(12),
            cancellationToken);

        var membership =
            await dbContext.HouseholdMembers
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.HouseholdId ==
                            household.Id &&
                        x.PersonId ==
                            actorPersonId &&
                        x.IsActive,
                    cancellationToken);

        if (membership is null)
        {
            return null;
        }

        var obligationRow =
            await (
                from obligation in dbContext.HouseholdContributionObligations
                    .AsNoTracking()
                join targetAccount in dbContext.HouseholdAccounts
                    .AsNoTracking()
                    on obligation.TargetHouseholdAccountId equals targetAccount.Id
                where
                    obligation.Id ==
                        obligationId &&
                    obligation.HouseholdId ==
                        household.Id &&
                    obligation.HouseholdMemberId ==
                        membership.Id &&
                    obligation.StatusCode !=
                        HouseholdContributionStatuses.Cancelled &&
                    obligation.StatusCode !=
                        HouseholdContributionStatuses.Corrected &&
                    targetAccount.IsActive
                select new
                {
                    Obligation = obligation,
                    TargetAccount = targetAccount
                })
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (obligationRow is null)
        {
            return null;
        }

        var outstandingMinor =
            Math.Max(
                0,
                obligationRow.Obligation.AmountMinor -
                obligationRow.Obligation.PaidAmountMinor);

        if (outstandingMinor <= 0)
        {
            return null;
        }

        var pendingExists =
            await dbContext.HouseholdContributionPaymentRequests
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.ObligationId ==
                            obligationId &&
                        x.StatusCode ==
                            HouseholdContributionPaymentStatuses.Pending,
                    cancellationToken);

        if (pendingExists)
        {
            throw new InvalidOperationException(
                "Dla tego zobowiązania wysłano już wpłatę oczekującą na akceptację administratora.");
        }

        var accounts =
            await dbContext.PersonalFinancialAccounts
                .AsNoTracking()
                .Where(x =>
                    x.OwnerPersonId ==
                        actorPersonId &&
                    x.IsActive &&
                    x.CurrencyCode ==
                        obligationRow.TargetAccount.CurrencyCode)
                .OrderBy(x =>
                    x.Name)
                .ToArrayAsync(
                    cancellationToken);

        var accountIds =
            accounts
                .Select(x =>
                    x.Id)
                .ToArray();

        var balances =
            await dbContext.PersonalFinancialTransactions
                .AsNoTracking()
                .Where(x =>
                    accountIds.Contains(
                        x.AccountId) &&
                    x.OwnerPersonId ==
                        actorPersonId)
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

        var sourceAccounts =
            accounts
                .Select(x =>
                    new HouseholdContributionPaymentSourceAccount(
                        x.Id,
                        x.Name,
                        PersonalAccountTypes.GetNamePl(
                            x.AccountTypeCode),
                        x.CurrencyCode,
                        PersonalFinanceMoney.FromMinorUnits(
                            balances.GetValueOrDefault(
                                x.Id))))
                .Where(x =>
                    x.Balance > 0m)
                .ToArray();

        return new HouseholdContributionPaymentForm(
            obligationRow.Obligation.Id,
            obligationRow.Obligation.PeriodKey,
            HouseholdFinanceMoney.FromMinorUnits(
                obligationRow.Obligation.AmountMinor),
            HouseholdFinanceMoney.FromMinorUnits(
                obligationRow.Obligation.PaidAmountMinor),
            HouseholdFinanceMoney.FromMinorUnits(
                outstandingMinor),
            obligationRow.Obligation.DueDateUtc,
            obligationRow.TargetAccount.CurrencyCode,
            obligationRow.TargetAccount.Name,
            sourceAccounts);
    }

    public async Task<Guid> SubmitContributionPaymentAsync(
        SubmitHouseholdContributionPaymentRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinanceHouseholdView,
            cancellationToken);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinancePersonalManageOwn,
            cancellationToken);

        if (request.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Amount),
                "Kwota wpłaty musi być większa od zera.");
        }

        var amountMinor =
            HouseholdFinanceMoney.ToMinorUnits(
                request.Amount);

        var actorPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        var household =
            await GetActiveHouseholdForPersonAsync(
                actorPersonId,
                cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Użytkownik nie jest przypisany do aktywnego gospodarstwa.");

        var membership =
            await dbContext.HouseholdMembers
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.HouseholdId ==
                            household.Id &&
                        x.PersonId ==
                            actorPersonId &&
                        x.IsActive,
                    cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Użytkownik nie jest aktywnym domownikiem tego gospodarstwa.");

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var obligation =
            await dbContext.HouseholdContributionObligations
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            request.ObligationId &&
                        x.HouseholdId ==
                            household.Id &&
                        x.HouseholdMemberId ==
                            membership.Id &&
                        x.StatusCode !=
                            HouseholdContributionStatuses.Cancelled &&
                        x.StatusCode !=
                            HouseholdContributionStatuses.Corrected,
                    cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Zobowiązanie nie istnieje albo nie należy do zalogowanego domownika.");

        var outstandingMinor =
            Math.Max(
                0,
                obligation.AmountMinor -
                obligation.PaidAmountMinor);

        if (outstandingMinor <= 0)
        {
            throw new InvalidOperationException(
                "To zobowiązanie jest już opłacone.");
        }

        if (amountMinor >
            outstandingMinor)
        {
            throw new InvalidOperationException(
                "Kwota wpłaty nie może być większa od pozostałej kwoty zobowiązania.");
        }

        var pendingExists =
            await dbContext.HouseholdContributionPaymentRequests
                .AnyAsync(
                    x =>
                        x.ObligationId ==
                            obligation.Id &&
                        x.StatusCode ==
                            HouseholdContributionPaymentStatuses.Pending,
                    cancellationToken);

        if (pendingExists)
        {
            throw new InvalidOperationException(
                "Dla tego zobowiązania istnieje już wpłata oczekująca na akceptację administratora.");
        }

        var targetAccount =
            await dbContext.HouseholdAccounts
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            obligation.TargetHouseholdAccountId &&
                        x.HouseholdId ==
                            household.Id &&
                        x.IsActive,
                    cancellationToken)
            ?? throw new InvalidOperationException(
                "Konto docelowe gospodarstwa nie jest aktywne.");

        var sourceAccount =
            await dbContext.PersonalFinancialAccounts
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            request.SourcePersonalAccountId &&
                        x.OwnerPersonId ==
                            actorPersonId &&
                        x.IsActive,
                    cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Wybrane prywatne konto nie istnieje, jest zamknięte albo nie należy do zalogowanego użytkownika.");

        if (!string.Equals(
                sourceAccount.CurrencyCode,
                targetAccount.CurrencyCode,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Waluta prywatnego konta i konta domu musi być zgodna.");
        }

        var sourceBalanceMinor =
            await dbContext.PersonalFinancialTransactions
                .Where(x =>
                    x.AccountId ==
                        sourceAccount.Id &&
                    x.OwnerPersonId ==
                        actorPersonId)
                .SumAsync(
                    x => x.AmountMinor,
                    cancellationToken);

        if (sourceBalanceMinor <
            amountMinor)
        {
            throw new InvalidOperationException(
                "Niewystarczające środki na wybranym koncie prywatnym. Wpłata nie została wysłana.");
        }

        var now =
            DateTime.UtcNow;

        var paymentRequest =
            new HouseholdContributionPaymentRequest
            {
                Id =
                    Guid.NewGuid(),
                HouseholdId =
                    household.Id,
                HouseholdMemberId =
                    membership.Id,
                ObligationId =
                    obligation.Id,
                SourcePersonalAccountId =
                    sourceAccount.Id,
                TargetHouseholdAccountId =
                    targetAccount.Id,
                AmountMinor =
                    amountMinor,
                StatusCode =
                    HouseholdContributionPaymentStatuses.Pending,
                SubmittedByUserId =
                    actorUserId,
                SubmittedAtUtc =
                    now
            };

        dbContext.HouseholdContributionPaymentRequests.Add(
            paymentRequest);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType:
                    "M04.4.ContributionPaymentSubmitted",
                EntityType:
                    "HouseholdContributionPaymentRequest",
                EntityId:
                    paymentRequest.Id.ToString(),
                ActorId:
                    actorUserId.ToString(),
                CorrelationId:
                    correlationId,
                Description:
                    "Domownik wysłał wpłatę składki do akceptacji. Kwota i prywatne konto źródłowe nie są zapisywane w audycie."),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return paymentRequest.Id;
    }

    public async Task ApproveContributionPaymentAsync(
        ReviewHouseholdContributionPaymentRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinanceHouseholdApprove,
            cancellationToken);

        var actorPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        var household =
            await GetActiveHouseholdForPersonAsync(
                actorPersonId,
                cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Administrator nie jest przypisany do aktywnego gospodarstwa.");

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var paymentRequest =
            await dbContext.HouseholdContributionPaymentRequests
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            request.PaymentRequestId &&
                        x.HouseholdId ==
                            household.Id,
                    cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Wpłata nie istnieje albo należy do innego gospodarstwa.");

        if (paymentRequest.StatusCode !=
            HouseholdContributionPaymentStatuses.Pending)
        {
            throw new InvalidOperationException(
                "Ta wpłata została już rozpatrzona.");
        }

        var obligation =
            await dbContext.HouseholdContributionObligations
                .SingleAsync(
                    x =>
                        x.Id ==
                            paymentRequest.ObligationId &&
                        x.HouseholdId ==
                            household.Id &&
                        x.HouseholdMemberId ==
                            paymentRequest.HouseholdMemberId,
                    cancellationToken);

        if (obligation.StatusCode ==
                HouseholdContributionStatuses.Cancelled ||
            obligation.StatusCode ==
                HouseholdContributionStatuses.Corrected)
        {
            throw new InvalidOperationException(
                "Nie można zatwierdzić wpłaty dla anulowanego lub skorygowanego zobowiązania.");
        }

        var outstandingMinor =
            Math.Max(
                0,
                obligation.AmountMinor -
                obligation.PaidAmountMinor);

        if (outstandingMinor <= 0 ||
            paymentRequest.AmountMinor >
                outstandingMinor)
        {
            throw new InvalidOperationException(
                "Kwota oczekującej wpłaty jest większa od aktualnie pozostałej kwoty zobowiązania.");
        }

        var membership =
            await dbContext.HouseholdMembers
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            paymentRequest.HouseholdMemberId &&
                        x.HouseholdId ==
                            household.Id &&
                        x.IsActive,
                    cancellationToken)
            ?? throw new InvalidOperationException(
                "Domownik nie jest już aktywnym członkiem gospodarstwa.");

        var sourceAccount =
            await dbContext.PersonalFinancialAccounts
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            paymentRequest.SourcePersonalAccountId &&
                        x.OwnerPersonId ==
                            membership.PersonId &&
                        x.IsActive,
                    cancellationToken)
            ?? throw new InvalidOperationException(
                "Prywatne konto źródłowe domownika nie jest już aktywne.");

        var targetAccount =
            await dbContext.HouseholdAccounts
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            paymentRequest.TargetHouseholdAccountId &&
                        x.HouseholdId ==
                            household.Id &&
                        x.IsActive,
                    cancellationToken)
            ?? throw new InvalidOperationException(
                "Konto docelowe gospodarstwa nie jest już aktywne.");

        if (!string.Equals(
                sourceAccount.CurrencyCode,
                targetAccount.CurrencyCode,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Waluta konta prywatnego i konta gospodarstwa nie jest zgodna.");
        }

        var sourceBalanceMinor =
            await dbContext.PersonalFinancialTransactions
                .Where(x =>
                    x.AccountId ==
                        sourceAccount.Id &&
                    x.OwnerPersonId ==
                        membership.PersonId)
                .SumAsync(
                    x => x.AmountMinor,
                    cancellationToken);

        if (sourceBalanceMinor <
            paymentRequest.AmountMinor)
        {
            throw new InvalidOperationException(
                "Domownik nie ma już wystarczających środków na wybranym koncie. Wpłata pozostaje oczekująca i nie została zaksięgowana.");
        }

        var now =
            DateTime.UtcNow;

        var personalTransactionId =
            Guid.NewGuid();

        var householdEntryId =
            Guid.NewGuid();

        dbContext.PersonalFinancialTransactions.Add(
            new PersonalFinancialTransaction
            {
                Id =
                    personalTransactionId,
                AccountId =
                    sourceAccount.Id,
                OwnerPersonId =
                    membership.PersonId,
                KindCode =
                    PersonalTransactionKinds.Expense,
                AmountMinor =
                    -paymentRequest.AmountMinor,
                OccurredAtUtc =
                    now,
                CategoryCode =
                    PersonalFinanceCategories.HouseholdContribution,
                Counterparty =
                    "Budżet domu",
                Description =
                    $"Składka na budżet domu za {obligation.PeriodKey}",
                CreatedByUserId =
                    paymentRequest.SubmittedByUserId,
                CreatedAtUtc =
                    now
            });

        dbContext.HouseholdEntries.Add(
            new HouseholdEntry
            {
                Id =
                    householdEntryId,
                HouseholdId =
                    household.Id,
                AccountId =
                    targetAccount.Id,
                EntryTypeCode =
                    HouseholdEntryTypes.MemberContribution,
                AmountMinor =
                    paymentRequest.AmountMinor,
                OccurredAtUtc =
                    now,
                CategoryCode =
                    HouseholdFinanceCategories.HouseholdIncome,
                Description =
                    $"Wpłata składki domownika za {obligation.PeriodKey}",
                SourceType =
                    "HouseholdContributionPayment",
                SourceId =
                    paymentRequest.Id.ToString(),
                CreatedByUserId =
                    actorUserId,
                CreatedAtUtc =
                    now
            });

        sourceAccount.UpdatedAtUtc =
            now;

        targetAccount.UpdatedAtUtc =
            now;

        obligation.PaidAmountMinor +=
            paymentRequest.AmountMinor;

        obligation.StatusCode =
            obligation.PaidAmountMinor >=
                    obligation.AmountMinor
                ? HouseholdContributionStatuses.Paid
                : HouseholdContributionStatuses.PartiallyPaid;

        obligation.UpdatedAtUtc =
            now;

        paymentRequest.StatusCode =
            HouseholdContributionPaymentStatuses.Approved;

        paymentRequest.ReviewedByUserId =
            actorUserId;

        paymentRequest.ReviewedAtUtc =
            now;

        paymentRequest.ReviewNote =
            NormalizeOptionalText(
                request.ReviewNote,
                "Notatka administratora",
                500);

        paymentRequest.PersonalTransactionId =
            personalTransactionId;

        paymentRequest.HouseholdEntryId =
            householdEntryId;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType:
                    "M04.4.ContributionPaymentApproved",
                EntityType:
                    "HouseholdContributionPaymentRequest",
                EntityId:
                    paymentRequest.Id.ToString(),
                ActorId:
                    actorUserId.ToString(),
                CorrelationId:
                    correlationId,
                Description:
                    "Administrator zaakceptował wpłatę składki. Operacja prywatna i wpływ na konto domu zostały zaksięgowane atomowo; kwota i prywatne konto nie są zapisywane w audycie."),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);
    }

    public async Task RejectContributionPaymentAsync(
        ReviewHouseholdContributionPaymentRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinanceHouseholdApprove,
            cancellationToken);

        var actorPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        var household =
            await GetActiveHouseholdForPersonAsync(
                actorPersonId,
                cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Administrator nie jest przypisany do aktywnego gospodarstwa.");

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var paymentRequest =
            await dbContext.HouseholdContributionPaymentRequests
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            request.PaymentRequestId &&
                        x.HouseholdId ==
                            household.Id,
                    cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Wpłata nie istnieje albo należy do innego gospodarstwa.");

        if (paymentRequest.StatusCode !=
            HouseholdContributionPaymentStatuses.Pending)
        {
            throw new InvalidOperationException(
                "Ta wpłata została już rozpatrzona.");
        }

        var now =
            DateTime.UtcNow;

        paymentRequest.StatusCode =
            HouseholdContributionPaymentStatuses.Rejected;

        paymentRequest.ReviewedByUserId =
            actorUserId;

        paymentRequest.ReviewedAtUtc =
            now;

        paymentRequest.ReviewNote =
            NormalizeOptionalText(
                request.ReviewNote,
                "Powód odrzucenia",
                500);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType:
                    "M04.4.ContributionPaymentRejected",
                EntityType:
                    "HouseholdContributionPaymentRequest",
                EntityId:
                    paymentRequest.Id.ToString(),
                ActorId:
                    actorUserId.ToString(),
                CorrelationId:
                    correlationId,
                Description:
                    "Administrator odrzucił oczekującą wpłatę składki. Salda nie zostały zmienione."),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);
    }

    private async Task ResolveContributionIncomeRulesAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var rules =
            await dbContext.HouseholdContributionRules
                .Where(x =>
                    x.HouseholdId ==
                        householdId &&
                    x.IsActive)
                .ToArrayAsync(
                    cancellationToken);

        if (rules.Length == 0)
        {
            return;
        }

        var memberIds =
            rules
                .Select(x =>
                    x.HouseholdMemberId)
                .Distinct()
                .ToArray();

        var memberPersonMap =
            await dbContext.HouseholdMembers
                .AsNoTracking()
                .Where(x =>
                    x.HouseholdId ==
                        householdId &&
                    x.IsActive &&
                    memberIds.Contains(
                        x.Id))
                .ToDictionaryAsync(
                    x => x.Id,
                    x => x.PersonId,
                    cancellationToken);

        var targetAccountIds =
            rules
                .Select(x =>
                    x.TargetHouseholdAccountId)
                .Distinct()
                .ToArray();

        var targetCurrencies =
            await dbContext.HouseholdAccounts
                .AsNoTracking()
                .Where(x =>
                    x.HouseholdId ==
                        householdId &&
                    targetAccountIds.Contains(
                        x.Id))
                .ToDictionaryAsync(
                    x => x.Id,
                    x => x.CurrencyCode,
                    cancellationToken);

        var personIds =
            memberPersonMap.Values
                .Distinct()
                .ToArray();

        var salaryRules =
            await dbContext.PersonalRecurringRules
                .AsNoTracking()
                .Where(x =>
                    personIds.Contains(
                        x.OwnerPersonId) &&
                    x.IsActive &&
                    x.KindCode ==
                        PersonalTransactionKinds.Income &&
                    x.FrequencyCode ==
                        PersonalRecurringFrequencies.Monthly &&
                    x.CategoryCode ==
                        PersonalFinanceCategories.Salary)
                .OrderBy(x =>
                    x.CreatedAtUtc)
                .ToArrayAsync(
                    cancellationToken);

        var salaryRulesByPerson =
            salaryRules
                .GroupBy(x =>
                    x.OwnerPersonId)
                .ToDictionary(
                    x => x.Key,
                    x => x.ToArray());

        var changed =
            false;

        foreach (var rule in rules)
        {
            if (!memberPersonMap.TryGetValue(
                    rule.HouseholdMemberId,
                    out var personId) ||
                !targetCurrencies.TryGetValue(
                    rule.TargetHouseholdAccountId,
                    out var targetCurrency))
            {
                continue;
            }

            PersonalRecurringRule? currentlyLinked =
                null;

            if (rule.IncomeRuleId.HasValue)
            {
                currentlyLinked =
                    salaryRules
                        .SingleOrDefault(x =>
                            x.Id ==
                                rule.IncomeRuleId.Value &&
                            x.OwnerPersonId ==
                                personId);
            }

            if (currentlyLinked is not null &&
                string.Equals(
                    currentlyLinked.CurrencyCode,
                    targetCurrency,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!salaryRulesByPerson.TryGetValue(
                    personId,
                    out var candidates) ||
                candidates.Length != 1)
            {
                if (rule.IncomeRuleId.HasValue)
                {
                    rule.IncomeRuleId =
                        null;
                    rule.UpdatedAtUtc =
                        DateTime.UtcNow;
                    changed =
                        true;
                }

                continue;
            }

            var candidate =
                candidates[0];

            if (!string.Equals(
                    candidate.CurrencyCode,
                    targetCurrency,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (rule.IncomeRuleId !=
                candidate.Id)
            {
                rule.IncomeRuleId =
                    candidate.Id;
                rule.UpdatedAtUtc =
                    DateTime.UtcNow;
                changed =
                    true;
            }
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(
                cancellationToken);
        }
    }

    private async Task GenerateContributionObligationsAsync(
        Guid householdId,
        DateTime generateThroughUtc,
        CancellationToken cancellationToken)
    {
        await ResolveContributionIncomeRulesAsync(
            householdId,
            cancellationToken);

        var rules =
            await dbContext.HouseholdContributionRules
                .Where(x =>
                    x.HouseholdId ==
                        householdId &&
                    x.IsActive)
                .ToArrayAsync(
                    cancellationToken);

        foreach (var rule in rules)
        {
            await GenerateContributionObligationsForRuleAsync(
                rule,
                generateThroughUtc,
                cancellationToken);
        }
    }

    private async Task GenerateContributionObligationsForRuleAsync(
        HouseholdContributionRule rule,
        DateTime generateThroughUtc,
        CancellationToken cancellationToken)
    {
        if (!rule.IncomeRuleId.HasValue)
        {
            return;
        }

        var occurrences =
            await dbContext.PersonalRecurringOccurrences
                .AsNoTracking()
                .Where(x =>
                    x.RecurringRuleId ==
                        rule.IncomeRuleId.Value &&
                    x.PlannedDateUtc <=
                        generateThroughUtc &&
                    x.StatusCode !=
                        PersonalRecurringOccurrenceStatuses.Cancelled)
                .OrderBy(x =>
                    x.PlannedDateUtc)
                .ToArrayAsync(
                    cancellationToken);

        if (occurrences.Length == 0)
        {
            return;
        }

        var existingPeriodKeys =
            await dbContext.HouseholdContributionObligations
                .AsNoTracking()
                .Where(x =>
                    x.ContributionRuleId ==
                        rule.Id)
                .Select(x =>
                    x.PeriodKey)
                .ToArrayAsync(
                    cancellationToken);

        var existing =
            existingPeriodKeys.ToHashSet(
                StringComparer.Ordinal);

        var now =
            DateTime.UtcNow;

        var validFromPeriodStart =
            new DateTime(
                rule.ValidFromUtc.Year,
                rule.ValidFromUtc.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        DateTime? validToPeriodStart =
            rule.ValidToUtc.HasValue
                ? new DateTime(
                    rule.ValidToUtc.Value.Year,
                    rule.ValidToUtc.Value.Month,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc)
                : null;

        foreach (var occurrence in occurrences)
        {
            var occurrencePeriodStart =
                new DateTime(
                    occurrence.PlannedDateUtc.Year,
                    occurrence.PlannedDateUtc.Month,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc);

            if (occurrencePeriodStart <
                validFromPeriodStart)
            {
                continue;
            }

            if (validToPeriodStart.HasValue &&
                occurrencePeriodStart >
                    validToPeriodStart.Value)
            {
                continue;
            }

            if (!existing.Add(
                    occurrence.PeriodKey))
            {
                continue;
            }

            long amountMinor;

            if (rule.ModeCode ==
                HouseholdContributionModes.FixedAmount)
            {
                amountMinor =
                    rule.FixedAmountMinor
                    ?? throw new InvalidOperationException(
                        "Reguła stałej składki nie ma zdefiniowanej kwoty.");
            }
            else
            {
                var percentageBasisPoints =
                    rule.PercentageBasisPoints
                    ?? throw new InvalidOperationException(
                        "Reguła procentowa nie ma zdefiniowanego procentu.");

                amountMinor =
                    HouseholdContributionMath
                        .CalculatePercentageAmountMinor(
                            occurrence.PlannedAmountMinor,
                            percentageBasisPoints);
            }

            dbContext.HouseholdContributionObligations.Add(
                new HouseholdContributionObligation
                {
                    Id =
                        Guid.NewGuid(),
                    ContributionRuleId =
                        rule.Id,
                    HouseholdId =
                        rule.HouseholdId,
                    HouseholdMemberId =
                        rule.HouseholdMemberId,
                    TargetHouseholdAccountId =
                        rule.TargetHouseholdAccountId,
                    PeriodKey =
                        occurrence.PeriodKey,
                    AmountMinor =
                        amountMinor,
                    PaidAmountMinor =
                        0,
                    DueDateUtc =
                        occurrence.PlannedDateUtc.Date
                            .AddDays(
                                rule.DueOffsetDays),
                    StatusCode =
                        HouseholdContributionStatuses.Pending,
                    ModeCode =
                        rule.ModeCode,
                    FixedAmountMinorSnapshot =
                        rule.FixedAmountMinor,
                    PercentageBasisPointsSnapshot =
                        rule.PercentageBasisPoints,
                    PlannedIncomeAmountMinor =
                        occurrence.PlannedAmountMinor,
                    IncomeRuleId =
                        rule.IncomeRuleId,
                    IncomeOccurrenceId =
                        occurrence.Id,
                    CreatedAtUtc =
                        now,
                    UpdatedAtUtc =
                        now
                });
        }

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    private static string ResolveContributionStatus(
        HouseholdContributionObligation obligation,
        DateTime todayUtc)
    {
        if (obligation.StatusCode ==
                HouseholdContributionStatuses.Cancelled ||
            obligation.StatusCode ==
                HouseholdContributionStatuses.Corrected)
        {
            return obligation.StatusCode;
        }

        if (obligation.PaidAmountMinor >=
            obligation.AmountMinor)
        {
            return HouseholdContributionStatuses.Paid;
        }

        if (obligation.PaidAmountMinor > 0)
        {
            return obligation.DueDateUtc.Date <
                    todayUtc
                ? HouseholdContributionStatuses.Overdue
                : HouseholdContributionStatuses.PartiallyPaid;
        }

        return obligation.DueDateUtc.Date <
                todayUtc
            ? HouseholdContributionStatuses.Overdue
            : HouseholdContributionStatuses.Pending;
    }

    private static string GetPersonDisplayName(
        Domio.Domain.Users.Person person) =>
        !string.IsNullOrWhiteSpace(
            person.DisplayName)
            ? person.DisplayName!
            : $"{person.FirstName} {person.LastName}".Trim();

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
