using System.Data;
using System.Data.Common;
using System.Globalization;
using Domio.Application.Auditing;
using Domio.Application.FamilyFinance;
using Domio.Domain.FamilyFinance;
using Domio.Infrastructure.Authorization;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Domio.Infrastructure.FamilyFinance;

public sealed class FamilyAreaItemManagementService(
    DomioDbContext dbContext,
    IAuditService auditService) : IFamilyAreaItemManagementService
{
    public async Task<EditFamilyAreaItemContext?> GetEditContextAsync(
        Guid ruleId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            FamilyFinancePermissions.Manage,
            cancellationToken);

        var actorPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        var row =
            await GetRuleContextAsync(
                ruleId,
                actorPersonId,
                cancellationToken);

        if (row is null)
        {
            return null;
        }

        var beneficiaries =
            await GetBeneficiariesAsync(
                row.FamilyGroupId,
                cancellationToken);

        return new EditFamilyAreaItemContext(
            row.FamilyGroupId,
            row.FamilyGroupName,
            row.AreaId,
            row.AreaName,
            row.RuleId,
            row.Name,
            row.CategoryCode,
            FamilyFinanceMoney.FromMinorUnits(
                row.PlannedAmountMinor),
            row.FrequencyCode,
            row.DueDateModeCode,
            row.DueDay,
            row.DaysBeforeEnd,
            row.BeneficiaryPersonId,
            row.ActiveFromUtc,
            row.ActiveToUtc,
            row.IsActive,
            beneficiaries);
    }

    public async Task UpdateAsync(
        UpdateFamilyAreaItemRequest request,
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

        var actorPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        var row =
            await GetRuleContextAsync(
                request.RuleId,
                actorPersonId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Nie znaleziono pozycji obszaru rodzinnego.");

        if (!row.IsActive)
        {
            throw new InvalidOperationException(
                "Zakończonej pozycji nie można edytować.");
        }

        var name =
            NormalizeRequiredText(
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
            await EnsureBeneficiaryAsync(
                row.FamilyGroupId,
                request.BeneficiaryPersonId.Value,
                cancellationToken);
        }

        var todayUtc = DateTime.UtcNow.Date;
        var remainsActive =
            !activeTo.HasValue ||
            activeTo.Value >= todayUtc;

        var now = DateTime.UtcNow;

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await ExecuteAsync(
            """
            UPDATE FamilyRecurringRules
            SET Name = $name,
                CategoryCode = $categoryCode,
                PlannedAmountMinor = $plannedAmountMinor,
                FrequencyCode = $frequencyCode,
                DueDay = $dueDay,
                BeneficiaryPersonId = $beneficiaryPersonId,
                ActiveFromUtc = $activeFromUtc,
                ActiveToUtc = $activeToUtc,
                IsActive = $isActive,
                UpdatedAtUtc = $updatedAtUtc
            WHERE Id = $ruleId
              AND AreaId = $areaId
              AND RuleTypeCode = $expenseType;
            """,
            [
                Param("$name", name),
                Param("$categoryCode", request.CategoryCode),
                Param("$plannedAmountMinor", amountMinor),
                Param("$frequencyCode", request.FrequencyCode),
                Param("$dueDay", storedDueDay),
                Param("$beneficiaryPersonId", request.BeneficiaryPersonId),
                Param("$activeFromUtc", activeFrom),
                Param("$activeToUtc", activeTo),
                Param("$isActive", remainsActive ? 1 : 0),
                Param("$updatedAtUtc", now),
                Param("$ruleId", request.RuleId),
                Param("$areaId", row.AreaId),
                Param("$expenseType", FamilyRecurringRuleTypes.Expense)
            ],
            cancellationToken);

        await ExecuteAsync(
            """
            INSERT INTO FamilyRecurringDueSchedules
                (RuleId, ModeCode, DaysBeforeEnd, CreatedAtUtc, UpdatedAtUtc)
            VALUES
                ($ruleId, $modeCode, $daysBeforeEnd, $now, $now)
            ON CONFLICT(RuleId) DO UPDATE SET
                ModeCode = excluded.ModeCode,
                DaysBeforeEnd = excluded.DaysBeforeEnd,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """,
            [
                Param("$ruleId", request.RuleId),
                Param("$modeCode", request.DueDateModeCode),
                Param(
                    "$daysBeforeEnd",
                    request.DueDateModeCode ==
                        FamilyRecurringDueDateModes.DaysBeforeEnd
                        ? request.DaysBeforeEnd
                        : null),
                Param("$now", now)
            ],
            cancellationToken);

        await RefreshOpenOccurrencesAsync(
            row.FamilyGroupId,
            request.RuleId,
            amountMinor,
            request.FrequencyCode,
            storedDueDay,
            request.DueDateModeCode,
            request.DaysBeforeEnd,
            request.BeneficiaryPersonId,
            activeFrom,
            activeTo,
            now,
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.9.2.FIX05.FamilyAreaItemUpdated",
                EntityType: "FamilyRecurringRule",
                EntityId: request.RuleId.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Zmieniono pozycję obszaru rodzinnego. Opłacone wystąpienia historyczne nie zostały nadpisane."),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);
    }

    public async Task EndAsync(
        Guid ruleId,
        DateTime endDateUtc,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            FamilyFinancePermissions.Manage,
            cancellationToken);

        var actorPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        var row =
            await GetRuleContextAsync(
                ruleId,
                actorPersonId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Nie znaleziono pozycji obszaru rodzinnego.");

        if (!row.IsActive)
        {
            throw new InvalidOperationException(
                "Pozycja jest już zakończona.");
        }

        var endDate =
            NormalizeUtcDate(
                endDateUtc);

        var effectiveEnd =
            endDate < row.ActiveFromUtc.Date
                ? row.ActiveFromUtc.Date
                : row.ActiveToUtc.HasValue &&
                  row.ActiveToUtc.Value.Date < endDate
                    ? row.ActiveToUtc.Value.Date
                    : endDate;

        var now = DateTime.UtcNow;

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var changed =
            await ExecuteAsync(
                """
                UPDATE FamilyRecurringRules
                SET IsActive = 0,
                    ActiveToUtc = $activeToUtc,
                    UpdatedAtUtc = $updatedAtUtc
                WHERE Id = $ruleId
                  AND AreaId = $areaId
                  AND RuleTypeCode = $expenseType
                  AND IsActive = 1;
                """,
                [
                    Param("$activeToUtc", effectiveEnd),
                    Param("$updatedAtUtc", now),
                    Param("$ruleId", ruleId),
                    Param("$areaId", row.AreaId),
                    Param("$expenseType", FamilyRecurringRuleTypes.Expense)
                ],
                cancellationToken);

        if (changed == 0)
        {
            throw new InvalidOperationException(
                "Pozycja jest już zakończona.");
        }

        await ExecuteAsync(
            """
            UPDATE FamilyRecurringOccurrences
            SET StatusCode = $cancelled,
                UpdatedAtUtc = $updatedAtUtc
            WHERE RuleId = $ruleId
              AND StatusCode = $planned
              AND PlannedDateUtc >= $endDateUtc
              AND NOT EXISTS
                  (
                      SELECT 1
                      FROM FamilyExpensePayments p
                      WHERE p.OccurrenceId = FamilyRecurringOccurrences.Id
                  );
            """,
            [
                Param("$cancelled", FamilyRecurringOccurrenceStatuses.Cancelled),
                Param("$updatedAtUtc", now),
                Param("$ruleId", ruleId),
                Param("$planned", FamilyRecurringOccurrenceStatuses.Planned),
                Param("$endDateUtc", effectiveEnd)
            ],
            cancellationToken);

        // Jeżeli zakończenie wypada przed planowanym terminem w miesiącu,
        // zapisujemy anulowane wystąpienie dla tego okresu. Dzięki unikalności
        // RuleId + PeriodKey generator nie odtworzy później kosztu, który został
        // świadomie zakończony przed terminem. Gdy termin już minął, bieżące
        // zobowiązanie pozostaje częścią historii tego miesiąca.
        var endMonthStart =
            MonthStart(
                effectiveEnd.Year,
                effectiveEnd.Month);

        if (ShouldGenerate(
                row.FrequencyCode,
                row.ActiveFromUtc,
                effectiveEnd,
                endMonthStart))
        {
            var plannedDateUtc =
                FamilyRecurringDueDateModes.ResolveDateUtc(
                    row.DueDateModeCode,
                    row.DueDay,
                    row.DaysBeforeEnd,
                    effectiveEnd.Year,
                    effectiveEnd.Month);

            if (plannedDateUtc.Date >= effectiveEnd.Date)
            {
                var periodKey =
                    $"{effectiveEnd.Year:D4}-{effectiveEnd.Month:D2}";

                await ExecuteAsync(
                    """
                    INSERT OR IGNORE INTO FamilyRecurringOccurrences
                        (Id, FamilyGroupId, RuleId, PeriodKey, PlannedDateUtc,
                         PlannedAmountMinor, StatusCode, BeneficiaryPersonId,
                         CreatedAtUtc, UpdatedAtUtc)
                    VALUES
                        ($id, $familyGroupId, $ruleId, $periodKey, $plannedDateUtc,
                         $plannedAmountMinor, $cancelled, $beneficiaryPersonId,
                         $createdAtUtc, $updatedAtUtc);
                    """,
                    [
                        Param("$id", Guid.NewGuid()),
                        Param("$familyGroupId", row.FamilyGroupId),
                        Param("$ruleId", ruleId),
                        Param("$periodKey", periodKey),
                        Param("$plannedDateUtc", plannedDateUtc),
                        Param("$plannedAmountMinor", row.PlannedAmountMinor),
                        Param("$cancelled", FamilyRecurringOccurrenceStatuses.Cancelled),
                        Param("$beneficiaryPersonId", row.BeneficiaryPersonId),
                        Param("$createdAtUtc", now),
                        Param("$updatedAtUtc", now)
                    ],
                    cancellationToken);
            }
        }

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.9.2.FIX05.FamilyAreaItemEnded",
                EntityType: "FamilyRecurringRule",
                EntityId: ruleId.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Zakończono pozycję obszaru rodzinnego. Historia oraz wcześniejsze płatności zostały zachowane."),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);
    }

    private async Task RefreshOpenOccurrencesAsync(
        Guid familyGroupId,
        Guid ruleId,
        long amountMinor,
        string frequencyCode,
        int storedDueDay,
        string dueDateModeCode,
        int? daysBeforeEnd,
        Guid? beneficiaryPersonId,
        DateTime activeFromUtc,
        DateTime? activeToUtc,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var currentPeriod =
            $"{DateTime.UtcNow.Year:D4}-{DateTime.UtcNow.Month:D2}";

        var occurrences =
            await GetOpenOccurrencesAsync(
                ruleId,
                currentPeriod,
                cancellationToken);

        foreach (var occurrence in occurrences)
        {
            if (!TryParsePeriodKey(
                    occurrence.PeriodKey,
                    out var year,
                    out var month))
            {
                continue;
            }

            var monthStart =
                new DateTime(
                    year,
                    month,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc);

            var shouldExist =
                ShouldGenerate(
                    frequencyCode,
                    activeFromUtc,
                    activeToUtc,
                    monthStart);

            if (!shouldExist)
            {
                await ExecuteAsync(
                    """
                    UPDATE FamilyRecurringOccurrences
                    SET StatusCode = $cancelled,
                        UpdatedAtUtc = $updatedAtUtc
                    WHERE Id = $id
                      AND FamilyGroupId = $familyGroupId
                      AND StatusCode = $planned;
                    """,
                    [
                        Param("$cancelled", FamilyRecurringOccurrenceStatuses.Cancelled),
                        Param("$updatedAtUtc", now),
                        Param("$id", occurrence.Id),
                        Param("$familyGroupId", familyGroupId),
                        Param("$planned", FamilyRecurringOccurrenceStatuses.Planned)
                    ],
                    cancellationToken);

                continue;
            }

            var plannedDateUtc =
                FamilyRecurringDueDateModes.ResolveDateUtc(
                    dueDateModeCode,
                    storedDueDay,
                    daysBeforeEnd,
                    year,
                    month);

            await ExecuteAsync(
                """
                UPDATE FamilyRecurringOccurrences
                SET PlannedDateUtc = $plannedDateUtc,
                    PlannedAmountMinor = $plannedAmountMinor,
                    BeneficiaryPersonId = $beneficiaryPersonId,
                    UpdatedAtUtc = $updatedAtUtc
                WHERE Id = $id
                  AND FamilyGroupId = $familyGroupId
                  AND StatusCode = $planned;
                """,
                [
                    Param("$plannedDateUtc", plannedDateUtc),
                    Param("$plannedAmountMinor", amountMinor),
                    Param("$beneficiaryPersonId", beneficiaryPersonId),
                    Param("$updatedAtUtc", now),
                    Param("$id", occurrence.Id),
                    Param("$familyGroupId", familyGroupId),
                    Param("$planned", FamilyRecurringOccurrenceStatuses.Planned)
                ],
                cancellationToken);
        }
    }

    private async Task<RuleContextRow?> GetRuleContextAsync(
        Guid ruleId,
        Guid actorPersonId,
        CancellationToken cancellationToken)
    {
        RuleContextRow? result = null;

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT
                        r.Id,
                        r.FamilyGroupId,
                        g.Name,
                        a.Id,
                        a.Name,
                        r.Name,
                        r.CategoryCode,
                        r.PlannedAmountMinor,
                        r.FrequencyCode,
                        r.DueDay,
                        r.BeneficiaryPersonId,
                        r.ActiveFromUtc,
                        r.ActiveToUtc,
                        r.IsActive,
                        schedule.ModeCode,
                        schedule.DaysBeforeEnd
                    FROM FamilyRecurringRules r
                    INNER JOIN FamilyBudgetAreas a
                        ON a.Id = r.AreaId
                    INNER JOIN FamilyGroups g
                        ON g.Id = r.FamilyGroupId
                    LEFT JOIN FamilyRecurringDueSchedules schedule
                        ON schedule.RuleId = r.Id
                    WHERE r.Id = $ruleId
                      AND r.RuleTypeCode = $expenseType
                      AND a.IsActive = 1
                      AND g.IsActive = 1
                      AND EXISTS
                          (
                              SELECT 1
                              FROM FamilyMembers fm
                              WHERE fm.FamilyGroupId = r.FamilyGroupId
                                AND fm.PersonId = $actorPersonId
                                AND fm.ValidToUtc IS NULL
                          )
                    LIMIT 1;
                    """;

                AddParameter(command, "$ruleId", ruleId);
                AddParameter(
                    command,
                    "$expenseType",
                    FamilyRecurringRuleTypes.Expense);
                AddParameter(
                    command,
                    "$actorPersonId",
                    actorPersonId);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                if (!await reader.ReadAsync(
                        cancellationToken))
                {
                    return;
                }

                result =
                    new RuleContextRow(
                        reader.GetGuid(0),
                        reader.GetGuid(1),
                        reader.GetString(2),
                        reader.GetGuid(3),
                        reader.GetString(4),
                        reader.GetString(5),
                        reader.GetString(6),
                        Convert.ToInt64(reader.GetValue(7)),
                        reader.GetString(8),
                        reader.GetInt32(9),
                        reader.IsDBNull(10)
                            ? null
                            : reader.GetGuid(10),
                        ReadDateTime(reader, 11),
                        reader.IsDBNull(12)
                            ? null
                            : ReadDateTime(reader, 12),
                        Convert.ToInt64(reader.GetValue(13)) != 0,
                        reader.IsDBNull(14)
                            ? FamilyRecurringDueDateModes.SpecificDay
                            : reader.GetString(14),
                        reader.IsDBNull(15)
                            ? null
                            : reader.GetInt32(15));
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
                    SELECT p.Id, p.DisplayName, p.FirstName, p.LastName,
                           fm.FamilyRoleCode
                    FROM FamilyMembers fm
                    INNER JOIN People p
                        ON p.Id = fm.PersonId
                    WHERE fm.FamilyGroupId = $familyGroupId
                      AND fm.ValidToUtc IS NULL
                      AND p.IsActive = 1
                    ORDER BY fm.FamilyRoleCode, p.DisplayName, p.FirstName, p.LastName;
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
                        new FamilyAreaBeneficiary(
                            reader.GetGuid(0),
                            BuildDisplayName(
                                reader.IsDBNull(1)
                                    ? null
                                    : reader.GetString(1),
                                reader.IsDBNull(2)
                                    ? string.Empty
                                    : reader.GetString(2),
                                reader.IsDBNull(3)
                                    ? string.Empty
                                    : reader.GetString(3)),
                            reader.GetString(4)));
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
        var count =
            await ScalarLongAsync(
                """
                SELECT COUNT(1)
                FROM FamilyMembers
                WHERE FamilyGroupId = $familyGroupId
                  AND PersonId = $personId
                  AND ValidToUtc IS NULL;
                """,
                [
                    Param("$familyGroupId", familyGroupId),
                    Param("$personId", personId)
                ],
                cancellationToken);

        if (count == 0)
        {
            throw new InvalidOperationException(
                "Osoba przypisana do kosztu musi być aktywnym członkiem tej rodziny.");
        }
    }

    private async Task<IReadOnlyList<OpenOccurrenceRow>>
        GetOpenOccurrencesAsync(
            Guid ruleId,
            string currentPeriod,
            CancellationToken cancellationToken)
    {
        var result =
            new List<OpenOccurrenceRow>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT o.Id, o.PeriodKey
                    FROM FamilyRecurringOccurrences o
                    WHERE o.RuleId = $ruleId
                      AND o.PeriodKey >= $currentPeriod
                      AND o.StatusCode = $planned
                      AND NOT EXISTS
                          (
                              SELECT 1
                              FROM FamilyExpensePayments p
                              WHERE p.OccurrenceId = o.Id
                          )
                    ORDER BY o.PeriodKey;
                    """;

                AddParameter(command, "$ruleId", ruleId);
                AddParameter(command, "$currentPeriod", currentPeriod);
                AddParameter(
                    command,
                    "$planned",
                    FamilyRecurringOccurrenceStatuses.Planned);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(
                    cancellationToken))
                {
                    result.Add(
                        new OpenOccurrenceRow(
                            reader.GetGuid(0),
                            reader.GetString(1)));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<Guid> GetActorPersonIdAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var actor =
            await (
                from account in dbContext.UserAccounts.AsNoTracking()
                join person in dbContext.People.AsNoTracking()
                    on account.PersonId equals person.Id
                where account.Id == actorUserId &&
                      account.IsActive &&
                      person.IsActive
                select person.Id)
                .SingleOrDefaultAsync(
                    cancellationToken);

        return actor != Guid.Empty
            ? actor
            : throw new UnauthorizedAccessException(
                "Nie można ustalić aktywnej osoby powiązanej z kontem.");
    }

    private static bool ShouldGenerate(
        string frequencyCode,
        DateTime activeFromUtc,
        DateTime? activeToUtc,
        DateTime monthStart)
    {
        var startMonth =
            MonthStart(
                activeFromUtc.Year,
                activeFromUtc.Month);

        if (monthStart < startMonth)
        {
            return false;
        }

        if (activeToUtc.HasValue)
        {
            var endMonth =
                MonthStart(
                    activeToUtc.Value.Year,
                    activeToUtc.Value.Month);

            if (monthStart > endMonth)
            {
                return false;
            }
        }

        var months =
            (monthStart.Year - startMonth.Year) * 12 +
            monthStart.Month - startMonth.Month;

        return frequencyCode switch
        {
            FamilyRecurringFrequencies.Once => months == 0,
            FamilyRecurringFrequencies.Monthly => true,
            FamilyRecurringFrequencies.Every2Months => months % 2 == 0,
            FamilyRecurringFrequencies.Quarterly => months % 3 == 0,
            FamilyRecurringFrequencies.Every4Months => months % 4 == 0,
            FamilyRecurringFrequencies.SemiAnnual => months % 6 == 0,
            FamilyRecurringFrequencies.Yearly => months % 12 == 0,
            _ => false
        };
    }

    private static DateTime MonthStart(
        int year,
        int month) =>
        new(
            year,
            month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

    private static bool TryParsePeriodKey(
        string periodKey,
        out int year,
        out int month)
    {
        year = 0;
        month = 0;

        if (periodKey.Length != 7 ||
            periodKey[4] != '-' ||
            !int.TryParse(
                periodKey.AsSpan(0, 4),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out year) ||
            !int.TryParse(
                periodKey.AsSpan(5, 2),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out month))
        {
            return false;
        }

        return year is >= 2000 and <= 9999 &&
               month is >= 1 and <= 12;
    }

    private static DateTime NormalizeUtcDate(
        DateTime value) =>
        new(
            value.Year,
            value.Month,
            value.Day,
            0,
            0,
            0,
            DateTimeKind.Utc);

    private static string NormalizeRequiredText(
        string? value,
        string fieldName,
        int maxLength)
    {
        var normalized =
            value?.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException(
                $"{fieldName} jest wymagane.");
        }

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
        string lastName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        return string.Join(
            " ",
            new[] { firstName, lastName }
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x)))
            .Trim();
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

                var value =
                    await command.ExecuteScalarAsync(
                        cancellationToken);

                result =
                    value is null || value is DBNull
                        ? 0
                        : Convert.ToInt64(value);
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
                dbContext.Database.CurrentTransaction is null)
            {
                await connection.CloseAsync();
            }
        }
    }

    private void AttachCurrentTransaction(
        DbCommand command)
    {
        var transaction =
            dbContext.Database.CurrentTransaction;

        if (transaction is not null)
        {
            command.Transaction =
                transaction.GetDbTransaction();
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
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static DateTime ReadDateTime(
        DbDataReader reader,
        int index)
    {
        var value =
            reader.GetValue(index);

        if (value is DateTime dateTime)
        {
            return dateTime.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(
                    dateTime,
                    DateTimeKind.Utc)
                : dateTime.ToUniversalTime();
        }

        var parsed =
            DateTime.Parse(
                Convert.ToString(
                    value,
                    CultureInfo.InvariantCulture)!,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);

        return parsed.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(
                parsed,
                DateTimeKind.Utc)
            : parsed.ToUniversalTime();
    }

    private static ParameterValue Param(
        string name,
        object? value) =>
        new(name, value);

    private sealed record ParameterValue(
        string Name,
        object? Value);

    private sealed record OpenOccurrenceRow(
        Guid Id,
        string PeriodKey);

    private sealed record RuleContextRow(
        Guid RuleId,
        Guid FamilyGroupId,
        string FamilyGroupName,
        Guid AreaId,
        string AreaName,
        string Name,
        string CategoryCode,
        long PlannedAmountMinor,
        string FrequencyCode,
        int DueDay,
        Guid? BeneficiaryPersonId,
        DateTime ActiveFromUtc,
        DateTime? ActiveToUtc,
        bool IsActive,
        string DueDateModeCode,
        int? DaysBeforeEnd);
}
