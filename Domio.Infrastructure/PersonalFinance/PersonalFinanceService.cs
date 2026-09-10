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

        return new PersonalFinanceOverview(
            ownerPersonId,
            summaries,
            recentTransactions);
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
