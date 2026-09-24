using System.Data;
using System.Data.Common;
using Domio.Application.Auditing;
using Domio.Application.FinancialGoals;
using Domio.Domain.FamilyFinance;
using Domio.Domain.FinancialGoals;
using Domio.Domain.Users;
using Domio.Infrastructure.Authorization;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Domio.Infrastructure.FinancialGoals;

public sealed class FinancialGoalService(
    DomioDbContext dbContext,
    IAuditService auditService) : IFinancialGoalService
{
    public async Task<FinancialGoalOverview> GetOverviewAsync(
        string scopeCode,
        Guid? familyGroupId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetScopeContextAsync(
            scopeCode,
            familyGroupId,
            actorUserId,
            requireManage: false,
            cancellationToken);

        var goals = await GetGoalSummariesAsync(
            scope,
            cancellationToken);

        var active = goals.Where(x => x.IsActive).ToArray();

        return new FinancialGoalOverview(
            ToPublicScope(scope),
            goals,
            active.Sum(x => x.TargetAmount),
            active.Sum(x => x.SavedAmount),
            active.Sum(x => x.RemainingAmount));
    }

    public async Task<FinancialGoalDetails?> GetDetailsAsync(
        Guid goalId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var row = await GetGoalRowAsync(
            goalId,
            cancellationToken);

        if (row is null)
        {
            return null;
        }

        var scope = await GetScopeContextAsync(
            row.ScopeCode,
            row.FamilyGroupId,
            actorUserId,
            requireManage: false,
            cancellationToken);

        EnsureGoalBelongsToScope(row, scope);

        var summary = await GetGoalSummaryAsync(
            row,
            cancellationToken);

        var contributions = await GetContributionsAsync(
            goalId,
            cancellationToken);

        return new FinancialGoalDetails(
            ToPublicScope(scope),
            summary,
            contributions);
    }

    public async Task<Guid> CreateAsync(
        CreateFinancialGoalRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scope = await GetScopeContextAsync(
            request.ScopeCode,
            request.FamilyGroupId,
            actorUserId,
            requireManage: true,
            cancellationToken);

        var name = NormalizeRequiredText(
            request.Name,
            "Nazwa celu",
            160);

        if (!FinancialGoalCategories.IsValid(request.CategoryCode))
        {
            throw new ArgumentException(
                "Wybierz poprawną kategorię celu.");
        }

        var targetAmountMinor =
            FinancialGoalMoney.ToMinorUnits(
                request.TargetAmount);

        var notes = NormalizeOptionalText(
            request.Notes,
            "Notatka",
            1000);

        DateTime? targetDateUtc =
            request.TargetDateUtc.HasValue
                ? NormalizeUtcDate(request.TargetDateUtc.Value)
                : null;

        if (targetDateUtc.HasValue &&
            targetDateUtc.Value.Date < DateTime.UtcNow.Date)
        {
            throw new ArgumentException(
                "Termin nowego celu nie może być wcześniejszy niż dzisiaj.");
        }

        long? initialAmountMinor = null;

        if (request.InitialAmount.HasValue &&
            request.InitialAmount.Value != 0m)
        {
            initialAmountMinor =
                FinancialGoalMoney.ToMinorUnits(
                    request.InitialAmount.Value);
        }

        var now = DateTime.UtcNow;
        var goalId = Guid.NewGuid();

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await ExecuteAsync(
            """
            INSERT INTO FinancialGoals
                (Id, ScopeCode, OwnerPersonId, HouseholdId, FamilyGroupId,
                 Name, CategoryCode, TargetAmountMinor, TargetDateUtc, Notes,
                 IsActive, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, ArchivedAtUtc)
            VALUES
                ($id, $scopeCode, $ownerPersonId, $householdId, $familyGroupId,
                 $name, $categoryCode, $targetAmountMinor, $targetDateUtc, $notes,
                 1, $actorUserId, $now, $now, NULL);
            """,
            [
                P("$id", goalId),
                P("$scopeCode", scope.ScopeCode),
                P("$ownerPersonId", scope.ScopeCode == FinancialGoalScopes.Personal
                    ? (Guid?)scope.PersonId
                    : null),
                P("$householdId", scope.ScopeCode == FinancialGoalScopes.Household
                    ? scope.HouseholdId
                    : null),
                P("$familyGroupId", scope.ScopeCode == FinancialGoalScopes.Family
                    ? scope.FamilyGroupId
                    : null),
                P("$name", name),
                P("$categoryCode", request.CategoryCode),
                P("$targetAmountMinor", targetAmountMinor),
                P("$targetDateUtc", targetDateUtc),
                P("$notes", notes),
                P("$actorUserId", actorUserId),
                P("$now", now)
            ],
            cancellationToken);

        if (initialAmountMinor.HasValue)
        {
            await ExecuteAsync(
                """
                INSERT INTO FinancialGoalContributions
                    (Id, GoalId, AmountMinor, ContributedAtUtc, Note,
                     CreatedByUserId, CreatedAtUtc)
                VALUES
                    ($id, $goalId, $amountMinor, $contributedAtUtc, $note,
                     $actorUserId, $createdAtUtc);
                """,
                [
                    P("$id", Guid.NewGuid()),
                    P("$goalId", goalId),
                    P("$amountMinor", initialAmountMinor.Value),
                    P("$contributedAtUtc", now.Date),
                    P("$note", "Kwota początkowa celu"),
                    P("$actorUserId", actorUserId),
                    P("$createdAtUtc", now)
                ],
                cancellationToken);
        }

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.9.1.FinancialGoalCreated",
                EntityType: "FinancialGoal",
                EntityId: goalId.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    $"Utworzono cel finansowy w zakresie {scope.ScopeCode}. Kwoty celu nie są zapisywane w audycie."),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return goalId;
    }

    public async Task UpdateAsync(
        UpdateFinancialGoalRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var row = await GetGoalRowAsync(
            request.GoalId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "Nie znaleziono celu finansowego.");

        var scope = await GetScopeContextAsync(
            row.ScopeCode,
            row.FamilyGroupId,
            actorUserId,
            requireManage: true,
            cancellationToken);

        EnsureGoalBelongsToScope(row, scope);

        if (!row.IsActive)
        {
            throw new InvalidOperationException(
                "Zarchiwizowanego celu nie można edytować.");
        }

        var name = NormalizeRequiredText(
            request.Name,
            "Nazwa celu",
            160);

        if (!FinancialGoalCategories.IsValid(request.CategoryCode))
        {
            throw new ArgumentException(
                "Wybierz poprawną kategorię celu.");
        }

        var targetAmountMinor =
            FinancialGoalMoney.ToMinorUnits(
                request.TargetAmount);

        var notes = NormalizeOptionalText(
            request.Notes,
            "Notatka",
            1000);

        DateTime? targetDateUtc =
            request.TargetDateUtc.HasValue
                ? NormalizeUtcDate(request.TargetDateUtc.Value)
                : null;

        var now = DateTime.UtcNow;

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await ExecuteAsync(
            """
            UPDATE FinancialGoals
            SET Name = $name,
                CategoryCode = $categoryCode,
                TargetAmountMinor = $targetAmountMinor,
                TargetDateUtc = $targetDateUtc,
                Notes = $notes,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $id
              AND IsActive = 1;
            """,
            [
                P("$name", name),
                P("$categoryCode", request.CategoryCode),
                P("$targetAmountMinor", targetAmountMinor),
                P("$targetDateUtc", targetDateUtc),
                P("$notes", notes),
                P("$updatedAtUtc", now),
                P("$id", request.GoalId)
            ],
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.9.1.FinancialGoalUpdated",
                EntityType: "FinancialGoal",
                EntityId: request.GoalId.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Zmieniono parametry celu finansowego. Kwoty i notatki nie są zapisywane w audycie."),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<Guid> AddContributionAsync(
        AddFinancialGoalContributionRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var row = await GetGoalRowAsync(
            request.GoalId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "Nie znaleziono celu finansowego.");

        var scope = await GetScopeContextAsync(
            row.ScopeCode,
            row.FamilyGroupId,
            actorUserId,
            requireManage: false,
            cancellationToken);

        EnsureGoalBelongsToScope(row, scope);

        if (!scope.CanContribute)
        {
            throw new UnauthorizedAccessException(
                "Nie masz uprawnienia do wpłacania na ten cel.");
        }

        if (!row.IsActive)
        {
            throw new InvalidOperationException(
                "Nie można dopisać wpłaty do zarchiwizowanego celu.");
        }

        var amountMinor =
            FinancialGoalMoney.ToMinorUnits(
                request.Amount);

        var contributedAtUtc =
            request.ContributedAtUtc == default
                ? DateTime.UtcNow.Date
                : NormalizeUtcDate(request.ContributedAtUtc);

        if (contributedAtUtc.Date > DateTime.UtcNow.Date)
        {
            throw new ArgumentException(
                "Data wpłaty na cel nie może być późniejsza niż dzisiaj.");
        }

        var note = NormalizeOptionalText(
            request.Note,
            "Opis wpłaty",
            500);

        var now = DateTime.UtcNow;
        var contributionId = Guid.NewGuid();

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await ExecuteAsync(
            """
            INSERT INTO FinancialGoalContributions
                (Id, GoalId, AmountMinor, ContributedAtUtc, Note,
                 CreatedByUserId, CreatedAtUtc)
            VALUES
                ($id, $goalId, $amountMinor, $contributedAtUtc, $note,
                 $actorUserId, $createdAtUtc);
            """,
            [
                P("$id", contributionId),
                P("$goalId", request.GoalId),
                P("$amountMinor", amountMinor),
                P("$contributedAtUtc", contributedAtUtc),
                P("$note", note),
                P("$actorUserId", actorUserId),
                P("$createdAtUtc", now)
            ],
            cancellationToken);

        await ExecuteAsync(
            """
            UPDATE FinancialGoals
            SET UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $goalId;
            """,
            [
                P("$updatedAtUtc", now),
                P("$goalId", request.GoalId)
            ],
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.9.1.FinancialGoalContributionAdded",
                EntityType: "FinancialGoalContribution",
                EntityId: contributionId.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Dodano wpłatę/rezerwę do celu finansowego. Kwota nie jest zapisywana w audycie i nie tworzy transakcji na rachunku."),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return contributionId;
    }

    public async Task ArchiveAsync(
        Guid goalId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var row = await GetGoalRowAsync(
            goalId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "Nie znaleziono celu finansowego.");

        var scope = await GetScopeContextAsync(
            row.ScopeCode,
            row.FamilyGroupId,
            actorUserId,
            requireManage: true,
            cancellationToken);

        EnsureGoalBelongsToScope(row, scope);

        if (!row.IsActive)
        {
            return;
        }

        var now = DateTime.UtcNow;

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await ExecuteAsync(
            """
            UPDATE FinancialGoals
            SET IsActive = 0,
                ArchivedAtUtc = $now,
                UpdatedAtUtc = $now
            WHERE Id = $id;
            """,
            [
                P("$now", now),
                P("$id", goalId)
            ],
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.9.1.FinancialGoalArchived",
                EntityType: "FinancialGoal",
                EntityId: goalId.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Zarchiwizowano cel finansowy. Historia wpłat pozostała zachowana."),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<FinancialGoalSummary>> GetGoalSummariesAsync(
        ScopeContext scope,
        CancellationToken cancellationToken)
    {
        var rows = new List<FinancialGoalSummary>();

        var (filterSql, parameter) = ScopeFilter(scope);

        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    $"""
                    SELECT g.Id, g.Name, g.CategoryCode, g.TargetAmountMinor,
                           g.TargetDateUtc, g.Notes, g.IsActive,
                           COALESCE(SUM(c.AmountMinor), 0) AS SavedAmountMinor,
                           COUNT(c.Id) AS ContributionsCount,
                           MAX(c.ContributedAtUtc) AS LastContributionAtUtc
                    FROM FinancialGoals g
                    LEFT JOIN FinancialGoalContributions c
                        ON c.GoalId = g.Id
                    WHERE g.ScopeCode = $scopeCode
                      AND {filterSql}
                    GROUP BY g.Id, g.Name, g.CategoryCode, g.TargetAmountMinor,
                             g.TargetDateUtc, g.Notes, g.IsActive
                    ORDER BY g.IsActive DESC, g.CreatedAtUtc DESC, g.Name;
                    """;

                AddParameter(command, "$scopeCode", scope.ScopeCode);
                AddParameter(command, parameter.Name, parameter.Value);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    rows.Add(ReadSummary(reader));
                }
            },
            cancellationToken);

        return rows
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.IsCompleted)
            .ThenBy(x => x.TargetDateUtc ?? DateTime.MaxValue)
            .ThenBy(x => x.Name)
            .ToArray();
    }

    private async Task<FinancialGoalSummary> GetGoalSummaryAsync(
        GoalRow row,
        CancellationToken cancellationToken)
    {
        long savedMinor = 0;
        int count = 0;
        DateTime? lastContributionAtUtc = null;

        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT COALESCE(SUM(AmountMinor), 0),
                           COUNT(Id),
                           MAX(ContributedAtUtc)
                    FROM FinancialGoalContributions
                    WHERE GoalId = $goalId;
                    """;
                AddParameter(command, "$goalId", row.Id);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                if (await reader.ReadAsync(cancellationToken))
                {
                    savedMinor = Convert.ToInt64(reader.GetValue(0));
                    count = Convert.ToInt32(reader.GetValue(1));
                    lastContributionAtUtc = reader.IsDBNull(2)
                        ? null
                        : ReadDateTime(reader, 2);
                }
            },
            cancellationToken);

        return BuildSummary(
            row.Id,
            row.Name,
            row.CategoryCode,
            row.TargetAmountMinor,
            row.TargetDateUtc,
            row.Notes,
            row.IsActive,
            savedMinor,
            count,
            lastContributionAtUtc);
    }

    private async Task<IReadOnlyList<FinancialGoalContributionSummary>> GetContributionsAsync(
        Guid goalId,
        CancellationToken cancellationToken)
    {
        var result = new List<FinancialGoalContributionSummary>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT
                        c.Id,
                        c.AmountMinor,
                        c.ContributedAtUtc,
                        c.Note,
                        c.CreatedAtUtc,
                        p.Id,
                        p.DisplayName,
                        p.FirstName,
                        p.LastName
                    FROM FinancialGoalContributions c
                    LEFT JOIN UserAccounts u
                        ON u.Id = c.CreatedByUserId
                    LEFT JOIN People p
                        ON p.Id = u.PersonId
                    WHERE c.GoalId = $goalId
                    ORDER BY c.ContributedAtUtc DESC, c.CreatedAtUtc DESC;
                    """;
                AddParameter(command, "$goalId", goalId);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    result.Add(
                        new FinancialGoalContributionSummary(
                            reader.GetGuid(0),
                            FinancialGoalMoney.FromMinorUnits(
                                Convert.ToInt64(reader.GetValue(1))),
                            ReadDateTime(reader, 2),
                            reader.IsDBNull(3) ? null : reader.GetString(3),
                            ReadDateTime(reader, 4),
                            reader.IsDBNull(5)
                                ? null
                                : reader.GetGuid(5),
                            reader.IsDBNull(5)
                                ? "Użytkownik Domio"
                                : BuildDisplayName(
                                    reader.IsDBNull(6)
                                        ? null
                                        : reader.GetString(6),
                                    reader.IsDBNull(7)
                                        ? string.Empty
                                        : reader.GetString(7),
                                    reader.IsDBNull(8)
                                        ? string.Empty
                                        : reader.GetString(8))));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<GoalRow?> GetGoalRowAsync(
        Guid goalId,
        CancellationToken cancellationToken)
    {
        GoalRow? result = null;

        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT Id, ScopeCode, OwnerPersonId, HouseholdId, FamilyGroupId,
                           Name, CategoryCode, TargetAmountMinor, TargetDateUtc,
                           Notes, IsActive
                    FROM FinancialGoals
                    WHERE Id = $id
                    LIMIT 1;
                    """;
                AddParameter(command, "$id", goalId);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                if (await reader.ReadAsync(cancellationToken))
                {
                    result = new GoalRow(
                        reader.GetGuid(0),
                        reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetGuid(2),
                        reader.IsDBNull(3) ? null : reader.GetGuid(3),
                        reader.IsDBNull(4) ? null : reader.GetGuid(4),
                        reader.GetString(5),
                        reader.GetString(6),
                        Convert.ToInt64(reader.GetValue(7)),
                        reader.IsDBNull(8) ? null : ReadDateTime(reader, 8),
                        reader.IsDBNull(9) ? null : reader.GetString(9),
                        Convert.ToInt64(reader.GetValue(10)) != 0);
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<ScopeContext> GetScopeContextAsync(
        string scopeCode,
        Guid? familyGroupId,
        Guid actorUserId,
        bool requireManage,
        CancellationToken cancellationToken)
    {
        if (!FinancialGoalScopes.IsValid(scopeCode))
        {
            throw new ArgumentException(
                "Wybrano nieprawidłowy zakres celu finansowego.");
        }

        var actor = await (
            from account in dbContext.UserAccounts.AsNoTracking()
            join person in dbContext.People.AsNoTracking()
                on account.PersonId equals person.Id
            where account.Id == actorUserId &&
                  account.IsActive &&
                  person.IsActive
            select new
            {
                PersonId = person.Id,
                person.DisplayName,
                person.FirstName,
                person.LastName
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Nie można ustalić aktywnego użytkownika.");

        var actorName = BuildDisplayName(
            actor.DisplayName,
            actor.FirstName,
            actor.LastName);

        var householdId = await dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(x =>
                x.PersonId == actor.PersonId &&
                x.IsActive)
            .Select(x => (Guid?)x.HouseholdId)
            .SingleOrDefaultAsync(cancellationToken);

        if (scopeCode == FinancialGoalScopes.Personal)
        {
            if (requireManage)
            {
                await PermissionEnforcement.EnsureUserHasAsync(
                    dbContext,
                    actorUserId,
                    SystemPermissions.FinancePersonalManageOwn,
                    cancellationToken);
            }
            else
            {
                await PermissionEnforcement.EnsureUserHasAsync(
                    dbContext,
                    actorUserId,
                    SystemPermissions.FinancePersonalViewOwn,
                    cancellationToken);
            }

            var canManage = requireManage ||
                await PermissionEnforcement.HasUserAsync(
                    dbContext,
                    actorUserId,
                    SystemPermissions.FinancePersonalManageOwn,
                    cancellationToken);

            return new ScopeContext(
                scopeCode,
                "Cele osobiste",
                actorName,
                actor.PersonId,
                householdId,
                null,
                canManage,
                canManage);
        }

        if (!householdId.HasValue)
        {
            throw new InvalidOperationException(
                "Brak aktywnego gospodarstwa.");
        }

        if (scopeCode == FinancialGoalScopes.Household)
        {
            if (requireManage)
            {
                await PermissionEnforcement.EnsureUserHasAsync(
                    dbContext,
                    actorUserId,
                    SystemPermissions.FinanceHouseholdManage,
                    cancellationToken);
            }
            else
            {
                await PermissionEnforcement.EnsureUserHasAsync(
                    dbContext,
                    actorUserId,
                    SystemPermissions.FinanceHouseholdView,
                    cancellationToken);
            }

            var householdName = await dbContext.Households
                .AsNoTracking()
                .Where(x => x.Id == householdId.Value && x.IsActive)
                .Select(x => x.Name)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    "Aktywne gospodarstwo nie istnieje.");

            var canManage = requireManage ||
                await PermissionEnforcement.HasUserAsync(
                    dbContext,
                    actorUserId,
                    SystemPermissions.FinanceHouseholdManage,
                    cancellationToken);

            return new ScopeContext(
                scopeCode,
                "Cele domu",
                householdName,
                actor.PersonId,
                householdId,
                null,
                canManage,
                true);
        }

        if (requireManage)
        {
            await PermissionEnforcement.EnsureUserHasAsync(
                dbContext,
                actorUserId,
                FamilyFinancePermissions.Manage,
                cancellationToken);
        }
        else
        {
            await PermissionEnforcement.EnsureUserHasAsync(
                dbContext,
                actorUserId,
                FamilyFinancePermissions.View,
                cancellationToken);
        }

        var family = await GetFamilyScopeAsync(
            householdId.Value,
            actor.PersonId,
            familyGroupId,
            cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Nie należysz do aktywnej grupy rodzinnej.");

        var familyCanManage = requireManage ||
            await PermissionEnforcement.HasUserAsync(
                dbContext,
                actorUserId,
                FamilyFinancePermissions.Manage,
                cancellationToken);

        return new ScopeContext(
            scopeCode,
            "Cele rodzinne",
            family.Name,
            actor.PersonId,
            householdId,
            family.Id,
            familyCanManage,
            true);
    }

    private async Task<FamilyScopeRow?> GetFamilyScopeAsync(
        Guid householdId,
        Guid personId,
        Guid? familyGroupId,
        CancellationToken cancellationToken)
    {
        FamilyScopeRow? result = null;

        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = familyGroupId.HasValue
                    ? """
                      SELECT g.Id, g.Name
                      FROM FamilyGroups g
                      INNER JOIN FamilyMembers m
                          ON m.FamilyGroupId = g.Id
                      WHERE g.Id = $familyGroupId
                        AND g.HouseholdId = $householdId
                        AND g.IsActive = 1
                        AND m.PersonId = $personId
                        AND m.ValidToUtc IS NULL
                      LIMIT 1;
                      """
                    : """
                      SELECT g.Id, g.Name
                      FROM FamilyGroups g
                      INNER JOIN FamilyMembers m
                          ON m.FamilyGroupId = g.Id
                      WHERE g.HouseholdId = $householdId
                        AND g.IsActive = 1
                        AND m.PersonId = $personId
                        AND m.ValidToUtc IS NULL
                      ORDER BY g.Name
                      LIMIT 1;
                      """;

                if (familyGroupId.HasValue)
                {
                    AddParameter(command, "$familyGroupId", familyGroupId.Value);
                }

                AddParameter(command, "$householdId", householdId);
                AddParameter(command, "$personId", personId);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                if (await reader.ReadAsync(cancellationToken))
                {
                    result = new FamilyScopeRow(
                        reader.GetGuid(0),
                        reader.GetString(1));
                }
            },
            cancellationToken);

        return result;
    }

    private static FinancialGoalScopeContext ToPublicScope(
        ScopeContext scope) =>
        new(
            scope.ScopeCode,
            scope.ScopeNamePl,
            scope.OwnerDisplayName,
            scope.FamilyGroupId,
            scope.CanManage,
            scope.CanContribute);

    private static (string Sql, ParameterValue Parameter) ScopeFilter(
        ScopeContext scope) =>
        scope.ScopeCode switch
        {
            FinancialGoalScopes.Personal =>
                ("g.OwnerPersonId = $scopeOwnerId",
                    P("$scopeOwnerId", scope.PersonId)),
            FinancialGoalScopes.Household =>
                ("g.HouseholdId = $scopeOwnerId",
                    P("$scopeOwnerId", scope.HouseholdId!.Value)),
            FinancialGoalScopes.Family =>
                ("g.FamilyGroupId = $scopeOwnerId",
                    P("$scopeOwnerId", scope.FamilyGroupId!.Value)),
            _ => throw new InvalidOperationException(
                "Nieobsługiwany zakres celu finansowego.")
        };

    private static void EnsureGoalBelongsToScope(
        GoalRow goal,
        ScopeContext scope)
    {
        var matches = goal.ScopeCode == scope.ScopeCode &&
            (scope.ScopeCode switch
            {
                FinancialGoalScopes.Personal =>
                    goal.OwnerPersonId == scope.PersonId,
                FinancialGoalScopes.Household =>
                    goal.HouseholdId == scope.HouseholdId,
                FinancialGoalScopes.Family =>
                    goal.FamilyGroupId == scope.FamilyGroupId,
                _ => false
            });

        if (!matches)
        {
            throw new UnauthorizedAccessException(
                "Ten cel finansowy nie należy do bieżącego zakresu.");
        }
    }

    private static FinancialGoalSummary ReadSummary(
        DbDataReader reader) =>
        BuildSummary(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            Convert.ToInt64(reader.GetValue(3)),
            reader.IsDBNull(4) ? null : ReadDateTime(reader, 4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            Convert.ToInt64(reader.GetValue(6)) != 0,
            Convert.ToInt64(reader.GetValue(7)),
            Convert.ToInt32(reader.GetValue(8)),
            reader.IsDBNull(9) ? null : ReadDateTime(reader, 9));

    private static FinancialGoalSummary BuildSummary(
        Guid id,
        string name,
        string categoryCode,
        long targetMinor,
        DateTime? targetDateUtc,
        string? notes,
        bool isActive,
        long savedMinor,
        int contributionsCount,
        DateTime? lastContributionAtUtc)
    {
        var target = FinancialGoalMoney.FromMinorUnits(targetMinor);
        var saved = FinancialGoalMoney.FromMinorUnits(savedMinor);
        var remaining = Math.Max(0m, target - saved);
        var progress = target <= 0m
            ? 0m
            : Math.Min(
                100m,
                decimal.Round(
                    saved / target * 100m,
                    1,
                    MidpointRounding.AwayFromZero));

        return new FinancialGoalSummary(
            id,
            name,
            categoryCode,
            FinancialGoalCategories.GetNamePl(categoryCode),
            target,
            saved,
            remaining,
            progress,
            targetDateUtc,
            notes,
            isActive,
            savedMinor >= targetMinor,
            contributionsCount,
            lastContributionAtUtc);
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

                result = await command.ExecuteNonQueryAsync(
                    cancellationToken);
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
            if (shouldClose &&
                dbContext.Database.CurrentTransaction is null)
            {
                await connection.CloseAsync();
            }
        }
    }

    private void AttachCurrentTransaction(DbCommand command)
    {
        var current = dbContext.Database.CurrentTransaction;

        if (current is not null)
        {
            command.Transaction = current.GetDbTransaction();
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

    private static ParameterValue P(
        string name,
        object? value) =>
        new(name, value);

    private static DateTime ReadDateTime(
        DbDataReader reader,
        int ordinal)
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

    private static DateTime NormalizeUtcDate(DateTime value) =>
        DateTime.SpecifyKind(
            value.Date,
            DateTimeKind.Utc);

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

    private static string BuildDisplayName(
        string? displayName,
        string firstName,
        string lastName) =>
        string.IsNullOrWhiteSpace(displayName)
            ? $"{firstName} {lastName}".Trim()
            : displayName.Trim();

    private sealed record ScopeContext(
        string ScopeCode,
        string ScopeNamePl,
        string OwnerDisplayName,
        Guid PersonId,
        Guid? HouseholdId,
        Guid? FamilyGroupId,
        bool CanManage,
        bool CanContribute);

    private sealed record FamilyScopeRow(
        Guid Id,
        string Name);

    private sealed record GoalRow(
        Guid Id,
        string ScopeCode,
        Guid? OwnerPersonId,
        Guid? HouseholdId,
        Guid? FamilyGroupId,
        string Name,
        string CategoryCode,
        long TargetAmountMinor,
        DateTime? TargetDateUtc,
        string? Notes,
        bool IsActive);

    private sealed record ParameterValue(
        string Name,
        object? Value);
}
