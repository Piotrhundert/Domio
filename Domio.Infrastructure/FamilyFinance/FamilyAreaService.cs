using System.Data;
using System.Data.Common;
using Domio.Application.Auditing;
using Domio.Application.FamilyFinance;
using Domio.Domain.FamilyFinance;
using Domio.Domain.HouseholdFinance;
using Domio.Domain.Users;
using Domio.Infrastructure.Authorization;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Domio.Infrastructure.FamilyFinance;

public sealed class FamilyAreaService(
    DomioDbContext dbContext,
    IAuditService auditService) : IFamilyAreaService
{
    public async Task<FamilyAreaOverview> GetOverviewAsync(
        Guid familyGroupId,
        Guid actorUserId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(year, month);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            FamilyFinancePermissions.View,
            cancellationToken);

        var actor = await GetActorAsync(
            actorUserId,
            cancellationToken);

        var family = await EnsureFamilyAsync(
            familyGroupId,
            actor.PersonId,
            actor.HouseholdId,
            cancellationToken);

        await EnsureDefaultAreasAsync(
            familyGroupId,
            actorUserId,
            cancellationToken);

        await NormalizeScheduledOccurrencesCoreAsync(
            family.Id,
            year,
            month,
            cancellationToken);

        var canManage =
            await PermissionEnforcement.HasUserAsync(
                dbContext,
                actorUserId,
                FamilyFinancePermissions.Manage,
                cancellationToken);

        var areaRows = await GetAreasAsync(
            familyGroupId,
            cancellationToken);

        var periodKey = BuildPeriodKey(year, month);
        var summaries = new List<FamilyAreaSummary>();

        foreach (var area in areaRows)
        {
            var statistics =
                area.SystemCode ==
                    FamilyAreaSystemCodes.HouseholdContributions
                    ? await GetContributionStatisticsAsync(
                        familyGroupId,
                        family.HouseholdId,
                        periodKey,
                        cancellationToken)
                    : await GetAreaStatisticsAsync(
                        familyGroupId,
                        area.Id,
                        periodKey,
                        cancellationToken);

            summaries.Add(
                BuildAreaSummary(
                    area,
                    statistics));
        }

        return new FamilyAreaOverview(
            family.Id,
            family.Name,
            year,
            month,
            canManage,
            summaries);
    }

    public async Task<FamilyAreaDetails?> GetDetailsAsync(
        Guid areaId,
        Guid actorUserId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(year, month);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            FamilyFinancePermissions.View,
            cancellationToken);

        var actor = await GetActorAsync(
            actorUserId,
            cancellationToken);

        var area = await GetAreaAsync(
            areaId,
            cancellationToken);

        if (area is null || !area.IsActive)
        {
            return null;
        }

        var family = await EnsureFamilyAsync(
            area.FamilyGroupId,
            actor.PersonId,
            actor.HouseholdId,
            cancellationToken);

        await EnsureDefaultAreasAsync(
            family.Id,
            actorUserId,
            cancellationToken);

        await NormalizeScheduledOccurrencesCoreAsync(
            family.Id,
            year,
            month,
            cancellationToken);

        var canManage =
            await PermissionEnforcement.HasUserAsync(
                dbContext,
                actorUserId,
                FamilyFinancePermissions.Manage,
                cancellationToken);

        var periodKey = BuildPeriodKey(year, month);

        AreaStatistics statistics;
        IReadOnlyList<FamilyAreaItemSummary> items = [];
        IReadOnlyList<FamilyAreaContributionSummary> contributions = [];

        if (area.SystemCode ==
            FamilyAreaSystemCodes.HouseholdContributions)
        {
            statistics =
                await GetContributionStatisticsAsync(
                    family.Id,
                    family.HouseholdId,
                    periodKey,
                    cancellationToken);

            contributions =
                await GetContributionDetailsAsync(
                    family.Id,
                    family.HouseholdId,
                    periodKey,
                    cancellationToken);
        }
        else
        {
            statistics =
                await GetAreaStatisticsAsync(
                    family.Id,
                    area.Id,
                    periodKey,
                    cancellationToken);

            items =
                await GetAreaItemsAsync(
                    family.Id,
                    area.Id,
                    periodKey,
                    cancellationToken);
        }

        return new FamilyAreaDetails(
            family.Id,
            family.Name,
            year,
            month,
            canManage,
            BuildAreaSummary(area, statistics),
            items,
            contributions);
    }

    public async Task NormalizeScheduledOccurrencesAsync(
        Guid familyGroupId,
        Guid actorUserId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(year, month);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            FamilyFinancePermissions.View,
            cancellationToken);

        var actor = await GetActorAsync(
            actorUserId,
            cancellationToken);

        var family = await EnsureFamilyAsync(
            familyGroupId,
            actor.PersonId,
            actor.HouseholdId,
            cancellationToken);

        await NormalizeScheduledOccurrencesCoreAsync(
            family.Id,
            year,
            month,
            cancellationToken);
    }

    public async Task<Guid> CreateAreaAsync(
        CreateFamilyAreaRequest request,
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

        var actor = await GetActorAsync(
            actorUserId,
            cancellationToken);

        await EnsureFamilyAsync(
            request.FamilyGroupId,
            actor.PersonId,
            actor.HouseholdId,
            cancellationToken);

        await EnsureDefaultAreasAsync(
            request.FamilyGroupId,
            actorUserId,
            cancellationToken);

        var name = NormalizeRequiredText(
            request.Name,
            "Nazwa obszaru",
            100);

        var duplicateCount =
            await ScalarLongAsync(
                """
                SELECT COUNT(1)
                FROM FamilyBudgetAreas
                WHERE FamilyGroupId = $familyGroupId
                  AND IsActive = 1
                  AND Name = $name COLLATE NOCASE;
                """,
                [
                    P("$familyGroupId", request.FamilyGroupId),
                    P("$name", name)
                ],
                cancellationToken);

        if (duplicateCount > 0)
        {
            throw new InvalidOperationException(
                "W tej rodzinie istnieje już aktywny obszar o takiej nazwie.");
        }

        var nextSortOrder =
            checked((int)await ScalarLongAsync(
                """
                SELECT COALESCE(MAX(SortOrder), 20) + 10
                FROM FamilyBudgetAreas
                WHERE FamilyGroupId = $familyGroupId;
                """,
                [P("$familyGroupId", request.FamilyGroupId)],
                cancellationToken));

        var now = DateTime.UtcNow;
        var areaId = Guid.NewGuid();

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await ExecuteAsync(
            """
            INSERT INTO FamilyBudgetAreas
                (Id, FamilyGroupId, Name, SystemCode, SortOrder,
                 IsActive, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc)
            VALUES
                ($id, $familyGroupId, $name, NULL, $sortOrder,
                 1, $actorUserId, $now, $now);
            """,
            [
                P("$id", areaId),
                P("$familyGroupId", request.FamilyGroupId),
                P("$name", name),
                P("$sortOrder", nextSortOrder),
                P("$actorUserId", actorUserId),
                P("$now", now)
            ],
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.9.2.FamilyAreaCreated",
                EntityType: "FamilyBudgetArea",
                EntityId: areaId.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Dodano własny obszar finansów rodzinnych. Obszar porządkuje koszty i nie tworzy osobnego księgowania."),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return areaId;
    }

    public async Task<CreateFamilyAreaItemContext?> GetCreateItemContextAsync(
        Guid areaId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            FamilyFinancePermissions.Manage,
            cancellationToken);

        var actor = await GetActorAsync(
            actorUserId,
            cancellationToken);

        var area = await GetAreaAsync(
            areaId,
            cancellationToken);

        if (area is null || !area.IsActive)
        {
            return null;
        }

        var family = await EnsureFamilyAsync(
            area.FamilyGroupId,
            actor.PersonId,
            actor.HouseholdId,
            cancellationToken);

        if (area.SystemCode ==
            FamilyAreaSystemCodes.HouseholdContributions)
        {
            throw new InvalidOperationException(
                "Do obszaru Składki do domu nie dodaje się ręcznych pozycji. Dane są pobierane automatycznie z modułu składek.");
        }

        var beneficiaries =
            await GetBeneficiariesAsync(
                family.Id,
                cancellationToken);

        return new CreateFamilyAreaItemContext(
            family.Id,
            family.Name,
            area.Id,
            area.Name,
            beneficiaries);
    }

    public async Task<Guid> CreateItemAsync(
        CreateFamilyAreaItemRequest request,
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

        var actor = await GetActorAsync(
            actorUserId,
            cancellationToken);

        var area = await GetAreaAsync(
            request.AreaId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "Nie znaleziono obszaru rodzinnego.");

        if (!area.IsActive)
        {
            throw new InvalidOperationException(
                "Nie można dodać pozycji do nieaktywnego obszaru.");
        }

        var family = await EnsureFamilyAsync(
            area.FamilyGroupId,
            actor.PersonId,
            actor.HouseholdId,
            cancellationToken);

        if (area.SystemCode ==
            FamilyAreaSystemCodes.HouseholdContributions)
        {
            throw new InvalidOperationException(
                "Składki do domu są zasilane automatycznie z modułu składek i nie mogą mieć ręcznych pozycji.");
        }

        var name = NormalizeRequiredText(
            request.Name,
            "Nazwa pozycji",
            160);

        if (!FamilyBudgetCategories.IsValid(
                request.CategoryCode))
        {
            throw new ArgumentException(
                "Wybierz poprawną kategorię rodzinną.");
        }

        if (!FamilyRecurringFrequencies.IsValid(
                request.FrequencyCode))
        {
            throw new ArgumentException(
                "Wybierz poprawną częstotliwość.");
        }

        var storedDueDay =
            FamilyRecurringDueDateModes.ResolveStoredDueDay(
                request.DueDateModeCode,
                request.DueDay,
                request.DaysBeforeEnd);

        var amountMinor =
            FamilyFinanceMoney.ToMinorUnits(
                request.PlannedAmount);

        var activeFrom =
            NormalizeUtcDate(
                request.ActiveFromUtc);

        DateTime? activeTo =
            request.ActiveToUtc.HasValue
                ? NormalizeUtcDate(
                    request.ActiveToUtc.Value)
                : null;

        if (activeTo.HasValue &&
            activeTo.Value < activeFrom)
        {
            throw new ArgumentException(
                "Data końcowa nie może być wcześniejsza od daty początku.");
        }

        if (request.FrequencyCode ==
            FamilyRecurringFrequencies.Once)
        {
            activeTo = activeFrom;
        }

        if (request.BeneficiaryPersonId.HasValue)
        {
            var beneficiaryCount =
                await ScalarLongAsync(
                    """
                    SELECT COUNT(1)
                    FROM FamilyMembers
                    WHERE FamilyGroupId = $familyGroupId
                      AND PersonId = $personId
                      AND ValidToUtc IS NULL;
                    """,
                    [
                        P("$familyGroupId", family.Id),
                        P("$personId", request.BeneficiaryPersonId.Value)
                    ],
                    cancellationToken);

            if (beneficiaryCount == 0)
            {
                throw new InvalidOperationException(
                    "Osoba przypisana do kosztu musi być aktywnym członkiem tej rodziny.");
            }
        }

        var ruleId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await ExecuteAsync(
            """
            INSERT INTO FamilyRecurringRules
                (Id, FamilyGroupId, Name, RuleTypeCode, CategoryCode,
                 PlannedAmountMinor, FrequencyCode, DueDay, BeneficiaryPersonId,
                 ActiveFromUtc, ActiveToUtc, IsActive, CreatedByUserId,
                 CreatedAtUtc, UpdatedAtUtc, AreaId)
            VALUES
                ($id, $familyGroupId, $name, $ruleTypeCode, $categoryCode,
                 $plannedAmountMinor, $frequencyCode, $dueDay, $beneficiaryPersonId,
                 $activeFromUtc, $activeToUtc, 1, $actorUserId,
                 $now, $now, $areaId);
            """,
            [
                P("$id", ruleId),
                P("$familyGroupId", family.Id),
                P("$name", name),
                P("$ruleTypeCode", FamilyRecurringRuleTypes.Expense),
                P("$categoryCode", request.CategoryCode),
                P("$plannedAmountMinor", amountMinor),
                P("$frequencyCode", request.FrequencyCode),
                P("$dueDay", storedDueDay),
                P("$beneficiaryPersonId", request.BeneficiaryPersonId),
                P("$activeFromUtc", activeFrom),
                P("$activeToUtc", activeTo),
                P("$actorUserId", actorUserId),
                P("$now", now),
                P("$areaId", area.Id)
            ],
            cancellationToken);

        await ExecuteAsync(
            """
            INSERT INTO FamilyRecurringDueSchedules
                (RuleId, ModeCode, DaysBeforeEnd, CreatedAtUtc, UpdatedAtUtc)
            VALUES
                ($ruleId, $modeCode, $daysBeforeEnd, $now, $now);
            """,
            [
                P("$ruleId", ruleId),
                P("$modeCode", request.DueDateModeCode),
                P("$daysBeforeEnd",
                    request.DueDateModeCode == FamilyRecurringDueDateModes.DaysBeforeEnd
                        ? request.DaysBeforeEnd
                        : null),
                P("$now", now)
            ],
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.9.2.FamilyAreaItemCreated",
                EntityType: "FamilyRecurringRule",
                EntityId: ruleId.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Dodano pozycję do obszaru rodzinnego. Pozycja korzysta z istniejącej reguły kosztu rodzinnego i nie tworzy równoległego budżetu."),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return ruleId;
    }

    private async Task EnsureDefaultAreasAsync(
        Guid familyGroupId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        await ExecuteAsync(
            """
            INSERT OR IGNORE INTO FamilyBudgetAreas
                (Id, FamilyGroupId, Name, SystemCode, SortOrder,
                 IsActive, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc)
            VALUES
                ($preschoolId, $familyGroupId, 'Przedszkole', $preschoolCode, 10,
                 1, $actorUserId, $now, $now),
                ($carId, $familyGroupId, 'Auto', $carCode, 20,
                 1, $actorUserId, $now, $now),
                ($contributionsId, $familyGroupId, 'Składki do domu', $contributionsCode, 30,
                 1, $actorUserId, $now, $now);
            """,
            [
                P("$preschoolId", Guid.NewGuid()),
                P("$carId", Guid.NewGuid()),
                P("$contributionsId", Guid.NewGuid()),
                P("$familyGroupId", familyGroupId),
                P("$preschoolCode", FamilyAreaSystemCodes.Preschool),
                P("$carCode", FamilyAreaSystemCodes.Car),
                P("$contributionsCode", FamilyAreaSystemCodes.HouseholdContributions),
                P("$actorUserId", actorUserId),
                P("$now", now)
            ],
            cancellationToken);
    }

    private async Task<IReadOnlyList<AreaRow>> GetAreasAsync(
        Guid familyGroupId,
        CancellationToken cancellationToken)
    {
        var result = new List<AreaRow>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT Id, FamilyGroupId, Name, SystemCode,
                           SortOrder, IsActive
                    FROM FamilyBudgetAreas
                    WHERE FamilyGroupId = $familyGroupId
                      AND IsActive = 1
                    ORDER BY SortOrder, Name;
                    """;

                AddParameter(
                    command,
                    "$familyGroupId",
                    familyGroupId);

                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(
                    cancellationToken))
                {
                    result.Add(
                        ReadArea(reader));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<AreaRow?> GetAreaAsync(
        Guid areaId,
        CancellationToken cancellationToken)
    {
        AreaRow? result = null;

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT Id, FamilyGroupId, Name, SystemCode,
                           SortOrder, IsActive
                    FROM FamilyBudgetAreas
                    WHERE Id = $id
                    LIMIT 1;
                    """;

                AddParameter(
                    command,
                    "$id",
                    areaId);

                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                if (await reader.ReadAsync(
                    cancellationToken))
                {
                    result = ReadArea(reader);
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<AreaStatistics> GetAreaStatisticsAsync(
        Guid familyGroupId,
        Guid areaId,
        string periodKey,
        CancellationToken cancellationToken)
    {
        AreaStatistics result = new(0, 0, 0);

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT
                        COALESCE(SUM(
                            CASE
                                WHEN o.Id IS NOT NULL
                                 AND o.StatusCode <> $cancelled
                                THEN o.PlannedAmountMinor
                                ELSE 0
                            END), 0),
                        COALESCE(SUM(
                            CASE
                                WHEN p.Id IS NOT NULL
                                THEN p.AmountMinor
                                ELSE 0
                            END), 0),
                        COUNT(DISTINCT CASE WHEN r.IsActive = 1 THEN r.Id END)
                    FROM FamilyRecurringRules r
                    LEFT JOIN FamilyRecurringOccurrences o
                        ON o.RuleId = r.Id
                       AND o.PeriodKey = $periodKey
                    LEFT JOIN FamilyExpensePayments p
                        ON p.OccurrenceId = o.Id
                    WHERE r.FamilyGroupId = $familyGroupId
                      AND r.AreaId = $areaId
                      AND r.RuleTypeCode = $expenseType;
                    """;

                AddParameter(command, "$cancelled",
                    FamilyRecurringOccurrenceStatuses.Cancelled);
                AddParameter(command, "$periodKey",
                    periodKey);
                AddParameter(command, "$familyGroupId",
                    familyGroupId);
                AddParameter(command, "$areaId",
                    areaId);
                AddParameter(command, "$expenseType",
                    FamilyRecurringRuleTypes.Expense);

                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                if (await reader.ReadAsync(
                    cancellationToken))
                {
                    result =
                        new AreaStatistics(
                            Convert.ToInt64(
                                reader.GetValue(0)),
                            Convert.ToInt64(
                                reader.GetValue(1)),
                            Convert.ToInt32(
                                reader.GetValue(2)));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<AreaStatistics> GetContributionStatisticsAsync(
        Guid familyGroupId,
        Guid householdId,
        string periodKey,
        CancellationToken cancellationToken)
    {
        AreaStatistics result = new(0, 0, 0);

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT
                        COALESCE(SUM(o.AmountMinor), 0),
                        COALESCE(SUM(o.PaidAmountMinor), 0),
                        COUNT(DISTINCT o.Id)
                    FROM HouseholdContributionObligations o
                    INNER JOIN HouseholdMembers hm
                        ON hm.Id = o.HouseholdMemberId
                    INNER JOIN FamilyMembers fm
                        ON fm.FamilyGroupId = $familyGroupId
                       AND fm.PersonId = hm.PersonId
                       AND fm.ValidToUtc IS NULL
                    WHERE o.HouseholdId = $householdId
                      AND o.PeriodKey = $periodKey
                      AND o.StatusCode <> $cancelled
                      AND o.StatusCode <> $corrected;
                    """;

                AddParameter(command, "$familyGroupId",
                    familyGroupId);
                AddParameter(command, "$householdId",
                    householdId);
                AddParameter(command, "$periodKey",
                    periodKey);
                AddParameter(command, "$cancelled",
                    HouseholdContributionStatuses.Cancelled);
                AddParameter(command, "$corrected",
                    HouseholdContributionStatuses.Corrected);

                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                if (await reader.ReadAsync(
                    cancellationToken))
                {
                    result =
                        new AreaStatistics(
                            Convert.ToInt64(
                                reader.GetValue(0)),
                            Convert.ToInt64(
                                reader.GetValue(1)),
                            Convert.ToInt32(
                                reader.GetValue(2)));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task NormalizeScheduledOccurrencesCoreAsync(
        Guid familyGroupId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var schedules = new List<DueScheduleRow>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT r.Id, r.DueDay, s.ModeCode, s.DaysBeforeEnd
                    FROM FamilyRecurringRules r
                    INNER JOIN FamilyRecurringDueSchedules s
                        ON s.RuleId = r.Id
                    WHERE r.FamilyGroupId = $familyGroupId
                      AND r.RuleTypeCode = $expenseType;
                    """;

                AddParameter(command, "$familyGroupId", familyGroupId);
                AddParameter(command, "$expenseType", FamilyRecurringRuleTypes.Expense);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    schedules.Add(
                        new DueScheduleRow(
                            reader.GetGuid(0),
                            reader.GetInt32(1),
                            reader.GetString(2),
                            reader.IsDBNull(3)
                                ? null
                                : reader.GetInt32(3)));
                }
            },
            cancellationToken);

        if (schedules.Count == 0)
        {
            return;
        }

        var periodKey = BuildPeriodKey(year, month);
        var now = DateTime.UtcNow;

        foreach (var schedule in schedules)
        {
            var plannedDateUtc =
                FamilyRecurringDueDateModes.ResolveDateUtc(
                    schedule.ModeCode,
                    schedule.StoredDueDay,
                    schedule.DaysBeforeEnd,
                    year,
                    month);

            await ExecuteAsync(
                """
                UPDATE FamilyRecurringOccurrences
                SET PlannedDateUtc = $plannedDateUtc,
                    UpdatedAtUtc = $updatedAtUtc
                WHERE FamilyGroupId = $familyGroupId
                  AND RuleId = $ruleId
                  AND PeriodKey = $periodKey;
                """,
                [
                    P("$plannedDateUtc", plannedDateUtc),
                    P("$updatedAtUtc", now),
                    P("$familyGroupId", familyGroupId),
                    P("$ruleId", schedule.RuleId),
                    P("$periodKey", periodKey)
                ],
                cancellationToken);
        }
    }

    private async Task<IReadOnlyList<FamilyAreaItemSummary>> GetAreaItemsAsync(
        Guid familyGroupId,
        Guid areaId,
        string periodKey,
        CancellationToken cancellationToken)
    {
        var result =
            new List<FamilyAreaItemSummary>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT
                        r.Id,
                        r.Name,
                        r.CategoryCode,
                        r.PlannedAmountMinor,
                        r.FrequencyCode,
                        r.DueDay,
                        r.BeneficiaryPersonId,
                        p.DisplayName,
                        p.FirstName,
                        p.LastName,
                        r.ActiveFromUtc,
                        r.ActiveToUtc,
                        r.IsActive,
                        o.Id,
                        o.PlannedDateUtc,
                        o.StatusCode,
                        COALESCE(pay.AmountMinor, 0),
                        schedule.ModeCode,
                        schedule.DaysBeforeEnd
                    FROM FamilyRecurringRules r
                    LEFT JOIN People p
                        ON p.Id = r.BeneficiaryPersonId
                    LEFT JOIN FamilyRecurringOccurrences o
                        ON o.RuleId = r.Id
                       AND o.PeriodKey = $periodKey
                    LEFT JOIN FamilyExpensePayments pay
                        ON pay.OccurrenceId = o.Id
                    LEFT JOIN FamilyRecurringDueSchedules schedule
                        ON schedule.RuleId = r.Id
                    WHERE r.FamilyGroupId = $familyGroupId
                      AND r.AreaId = $areaId
                      AND r.RuleTypeCode = $expenseType
                    ORDER BY r.IsActive DESC, r.Name;
                    """;

                AddParameter(command, "$periodKey",
                    periodKey);
                AddParameter(command, "$familyGroupId",
                    familyGroupId);
                AddParameter(command, "$areaId",
                    areaId);
                AddParameter(command, "$expenseType",
                    FamilyRecurringRuleTypes.Expense);

                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(
                    cancellationToken))
                {
                    Guid? occurrenceId =
                        reader.IsDBNull(13)
                            ? null
                            : reader.GetGuid(13);

                    DateTime? plannedDate =
                        reader.IsDBNull(14)
                            ? null
                            : ReadDateTime(reader, 14);

                    var occurrenceStatus =
                        reader.IsDBNull(15)
                            ? null
                            : reader.GetString(15);

                    var actualPaidMinor =
                        Convert.ToInt64(
                            reader.GetValue(16));

                    var beneficiaryName =
                        reader.IsDBNull(6)
                            ? null
                            : BuildDisplayName(
                                reader.IsDBNull(7)
                                    ? null
                                    : reader.GetString(7),
                                reader.IsDBNull(8)
                                    ? string.Empty
                                    : reader.GetString(8),
                                reader.IsDBNull(9)
                                    ? string.Empty
                                    : reader.GetString(9));

                    var canPay =
                        occurrenceId.HasValue &&
                        occurrenceStatus ==
                            FamilyRecurringOccurrenceStatuses.Planned &&
                        actualPaidMinor == 0 &&
                        plannedDate.HasValue &&
                        plannedDate.Value.Date <=
                            DateTime.UtcNow.Date;

                    result.Add(
                        new FamilyAreaItemSummary(
                            reader.GetGuid(0),
                            reader.GetString(1),
                            reader.GetString(2),
                            FamilyBudgetCategories.GetNamePl(
                                reader.GetString(2)),
                            FamilyFinanceMoney.FromMinorUnits(
                                Convert.ToInt64(
                                    reader.GetValue(3))),
                            reader.GetString(4),
                            FamilyRecurringFrequencies.GetNamePl(
                                reader.GetString(4)),
                            reader.GetInt32(5),
                            FamilyRecurringDueDateModes.GetDescription(
                                reader.IsDBNull(17)
                                    ? null
                                    : reader.GetString(17),
                                reader.GetInt32(5),
                                reader.IsDBNull(18)
                                    ? null
                                    : reader.GetInt32(18)),
                            reader.IsDBNull(6)
                                ? null
                                : reader.GetGuid(6),
                            beneficiaryName,
                            ReadDateTime(reader, 10),
                            reader.IsDBNull(11)
                                ? null
                                : ReadDateTime(reader, 11),
                            Convert.ToInt64(
                                reader.GetValue(12)) != 0,
                            occurrenceId,
                            plannedDate,
                            occurrenceStatus,
                            occurrenceStatus is null
                                ? "Nie przypada w tym miesiącu"
                                : FamilyRecurringOccurrenceStatuses.GetNamePl(
                                    occurrenceStatus),
                            FamilyFinanceMoney.FromMinorUnits(
                                actualPaidMinor),
                            canPay));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<IReadOnlyList<FamilyAreaContributionSummary>>
        GetContributionDetailsAsync(
            Guid familyGroupId,
            Guid householdId,
            string periodKey,
            CancellationToken cancellationToken)
    {
        var result =
            new List<FamilyAreaContributionSummary>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT
                        o.Id,
                        hm.PersonId,
                        p.DisplayName,
                        p.FirstName,
                        p.LastName,
                        fm.FamilyRoleCode,
                        o.PeriodKey,
                        o.AmountMinor,
                        o.PaidAmountMinor,
                        o.DueDateUtc,
                        o.StatusCode
                    FROM HouseholdContributionObligations o
                    INNER JOIN HouseholdMembers hm
                        ON hm.Id = o.HouseholdMemberId
                    INNER JOIN People p
                        ON p.Id = hm.PersonId
                    INNER JOIN FamilyMembers fm
                        ON fm.FamilyGroupId = $familyGroupId
                       AND fm.PersonId = hm.PersonId
                       AND fm.ValidToUtc IS NULL
                    WHERE o.HouseholdId = $householdId
                      AND o.PeriodKey = $periodKey
                      AND o.StatusCode <> $cancelled
                      AND o.StatusCode <> $corrected
                    ORDER BY p.FirstName, p.LastName;
                    """;

                AddParameter(command, "$familyGroupId",
                    familyGroupId);
                AddParameter(command, "$householdId",
                    householdId);
                AddParameter(command, "$periodKey",
                    periodKey);
                AddParameter(command, "$cancelled",
                    HouseholdContributionStatuses.Cancelled);
                AddParameter(command, "$corrected",
                    HouseholdContributionStatuses.Corrected);

                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(
                    cancellationToken))
                {
                    var amountMinor =
                        Convert.ToInt64(
                            reader.GetValue(7));
                    var paidMinor =
                        Convert.ToInt64(
                            reader.GetValue(8));
                    var dueDate =
                        ReadDateTime(
                            reader,
                            9);
                    var persistedStatus =
                        reader.GetString(10);

                    var resolvedStatus =
                        persistedStatus;

                    if (paidMinor < amountMinor &&
                        dueDate.Date < DateTime.UtcNow.Date &&
                        persistedStatus !=
                            HouseholdContributionStatuses.Paid)
                    {
                        resolvedStatus =
                            HouseholdContributionStatuses.Overdue;
                    }

                    result.Add(
                        new FamilyAreaContributionSummary(
                            reader.GetGuid(0),
                            reader.GetGuid(1),
                            BuildDisplayName(
                                reader.IsDBNull(2)
                                    ? null
                                    : reader.GetString(2),
                                reader.GetString(3),
                                reader.GetString(4)),
                            reader.GetString(5),
                            FamilyRoles.GetNamePl(
                                reader.GetString(5)),
                            reader.GetString(6),
                            FamilyFinanceMoney.FromMinorUnits(
                                amountMinor),
                            FamilyFinanceMoney.FromMinorUnits(
                                paidMinor),
                            FamilyFinanceMoney.FromMinorUnits(
                                Math.Max(
                                    0,
                                    amountMinor - paidMinor)),
                            dueDate,
                            resolvedStatus,
                            HouseholdContributionStatuses.GetNamePl(
                                resolvedStatus)));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<IReadOnlyList<FamilyAreaBeneficiary>>
        GetBeneficiariesAsync(
            Guid familyGroupId,
            CancellationToken cancellationToken)
    {
        var result =
            new List<FamilyAreaBeneficiary>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT
                        m.PersonId,
                        p.DisplayName,
                        p.FirstName,
                        p.LastName,
                        m.FamilyRoleCode
                    FROM FamilyMembers m
                    INNER JOIN People p
                        ON p.Id = m.PersonId
                    WHERE m.FamilyGroupId = $familyGroupId
                      AND m.ValidToUtc IS NULL
                      AND p.IsActive = 1
                    ORDER BY
                        CASE WHEN m.FamilyRoleCode = 'Child' THEN 0 ELSE 1 END,
                        p.FirstName,
                        p.LastName;
                    """;

                AddParameter(command, "$familyGroupId",
                    familyGroupId);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(
                    cancellationToken))
                {
                    result.Add(
                        new FamilyAreaBeneficiary(
                            reader.GetGuid(0),
                            BuildDisplayName(
                                reader.IsDBNull(1)
                                    ? null
                                    : reader.GetString(1),
                                reader.GetString(2),
                                reader.GetString(3)),
                            reader.GetString(4)));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<ActorRow> GetActorAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var actor =
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
                select new
                {
                    PersonId = person.Id
                })
                .SingleOrDefaultAsync(
                    cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Nie można ustalić aktywnego użytkownika.");

        var householdId =
            await dbContext.HouseholdMembers
                .AsNoTracking()
                .Where(x =>
                    x.PersonId ==
                        actor.PersonId &&
                    x.IsActive)
                .Select(x =>
                    (Guid?)x.HouseholdId)
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (!householdId.HasValue)
        {
            throw new InvalidOperationException(
                "Brak aktywnego gospodarstwa.");
        }

        return new ActorRow(
            actor.PersonId,
            householdId.Value);
    }

    private async Task<FamilyRow> EnsureFamilyAsync(
        Guid familyGroupId,
        Guid personId,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        FamilyRow? result = null;

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
                    WHERE g.Id = $familyGroupId
                      AND g.HouseholdId = $householdId
                      AND g.IsActive = 1
                      AND m.PersonId = $personId
                      AND m.ValidToUtc IS NULL
                    LIMIT 1;
                    """;

                AddParameter(command, "$familyGroupId",
                    familyGroupId);
                AddParameter(command, "$householdId",
                    householdId);
                AddParameter(command, "$personId",
                    personId);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                if (await reader.ReadAsync(
                    cancellationToken))
                {
                    result =
                        new FamilyRow(
                            reader.GetGuid(0),
                            reader.GetGuid(1),
                            reader.GetString(2));
                }
            },
            cancellationToken);

        return result
            ?? throw new UnauthorizedAccessException(
                "Dostęp do obszarów wymaga aktywnego członkostwa w tej rodzinie.");
    }

    private static FamilyAreaSummary BuildAreaSummary(
        AreaRow area,
        AreaStatistics statistics) =>
        new(
            area.Id,
            area.Name,
            area.SystemCode,
            FamilyAreaSystemCodes.GetDescriptionPl(
                area.SystemCode),
            FamilyAreaSystemCodes.GetBadge(
                area.SystemCode),
            area.SortOrder,
            !string.IsNullOrWhiteSpace(
                area.SystemCode),
            area.IsActive,
            statistics.ItemsCount,
            FamilyFinanceMoney.FromMinorUnits(
                statistics.PlannedAmountMinor),
            FamilyFinanceMoney.FromMinorUnits(
                statistics.ActualAmountMinor));

    private static AreaRow ReadArea(
        DbDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.IsDBNull(3)
                ? null
                : reader.GetString(3),
            reader.GetInt32(4),
            Convert.ToInt64(
                reader.GetValue(5)) != 0);

    private async Task<long> ScalarLongAsync(
        string sql,
        IReadOnlyList<ParameterValue> parameters,
        CancellationToken cancellationToken)
    {
        long result = 0;

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText = sql;

                foreach (var parameter in parameters)
                {
                    AddParameter(
                        command,
                        parameter.Name,
                        parameter.Value);
                }

                AttachCurrentTransaction(command);

                result =
                    Convert.ToInt64(
                        await command.ExecuteScalarAsync(
                            cancellationToken)
                        ?? 0);
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
                await using var command =
                    connection.CreateCommand();

                command.CommandText = sql;

                foreach (var parameter in parameters)
                {
                    AddParameter(
                        command,
                        parameter.Name,
                        parameter.Value);
                }

                AttachCurrentTransaction(command);

                result =
                    await command.ExecuteNonQueryAsync(
                        cancellationToken);
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
            connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(
                cancellationToken);
        }

        try
        {
            await action(connection);
        }
        finally
        {
            if (shouldClose &&
                dbContext.Database.CurrentTransaction
                    is null)
            {
                await connection.CloseAsync();
            }
        }
    }

    private void AttachCurrentTransaction(
        DbCommand command)
    {
        var current =
            dbContext.Database.CurrentTransaction;

        if (current is not null)
        {
            command.Transaction =
                current.GetDbTransaction();
        }
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        object? value)
    {
        var parameter =
            command.CreateParameter();

        parameter.ParameterName = name;
        parameter.Value =
            value ?? DBNull.Value;

        command.Parameters.Add(parameter);
    }

    private static ParameterValue P(
        string name,
        object? value) =>
        new(name, value);

    private static string BuildPeriodKey(
        int year,
        int month) =>
        $"{year:D4}-{month:D2}";

    private static DateTime NormalizeUtcDate(
        DateTime value) =>
        DateTime.SpecifyKind(
            value.Date,
            DateTimeKind.Utc);

    private static DateTime ReadDateTime(
        DbDataReader reader,
        int ordinal)
    {
        var value =
            reader.GetValue(ordinal);

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
            Convert.ToDateTime(value);

        return DateTime.SpecifyKind(
            parsed,
            DateTimeKind.Utc);
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
        string.IsNullOrWhiteSpace(
            displayName)
            ? $"{firstName} {lastName}".Trim()
            : displayName.Trim();

    private static void ValidatePeriod(
        int year,
        int month)
    {
        if (year < 2000 ||
            year > 2200 ||
            month < 1 ||
            month > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(year),
                "Nieprawidłowy okres.");
        }
    }

    private sealed record DueScheduleRow(
        Guid RuleId,
        int StoredDueDay,
        string ModeCode,
        int? DaysBeforeEnd);

    private sealed record ActorRow(
        Guid PersonId,
        Guid HouseholdId);

    private sealed record FamilyRow(
        Guid Id,
        Guid HouseholdId,
        string Name);

    private sealed record AreaRow(
        Guid Id,
        Guid FamilyGroupId,
        string Name,
        string? SystemCode,
        int SortOrder,
        bool IsActive);

    private sealed record AreaStatistics(
        long PlannedAmountMinor,
        long ActualAmountMinor,
        int ItemsCount);

    private sealed record ParameterValue(
        string Name,
        object? Value);
}
