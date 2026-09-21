using System.Data;
using System.Data.Common;
using Domio.Application.Auditing;
using Domio.Application.FamilyFinance;
using Domio.Domain.FamilyFinance;
using Domio.Domain.PersonalFinance;
using Domio.Infrastructure.Authorization;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Domio.Infrastructure.FamilyFinance;

public sealed class FamilyFinanceService(
    DomioDbContext dbContext,
    IAuditService auditService) : IFamilyFinanceService
{
    public async Task<FamilyFinanceOverview> GetOverviewAsync(
        Guid actorUserId,
        Guid? familyGroupId,
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

        var actor =
            await GetActorContextAsync(
                actorUserId,
                cancellationToken);

        if (actor.HouseholdId is null)
        {
            return new FamilyFinanceOverview(
                null,
                null,
                actor.PersonId,
                year,
                month,
                [],
                null,
                null,
                null,
                [],
                0m,
                0m,
                [],
                null);
        }

        var groups =
            await GetAccessibleGroupsAsync(
                actor.HouseholdId.Value,
                actor.PersonId,
                cancellationToken);

        FamilyGroupChoice? selectedGroup = null;

        if (familyGroupId.HasValue)
        {
            selectedGroup =
                groups.SingleOrDefault(x =>
                    x.FamilyGroupId == familyGroupId.Value)
                ?? throw new UnauthorizedAccessException(
                    "Nie należysz do wskazanej grupy rodzinnej.");
        }
        else
        {
            selectedGroup = groups.FirstOrDefault();
        }

        if (selectedGroup is null)
        {
            return new FamilyFinanceOverview(
                actor.HouseholdId,
                actor.HouseholdName,
                actor.PersonId,
                year,
                month,
                groups,
                null,
                null,
                null,
                [],
                0m,
                0m,
                [],
                null);
        }

        var members =
            await GetActiveMembersAsync(
                selectedGroup.FamilyGroupId,
                cancellationToken);

        var monthStart =
            new DateTime(
                year,
                month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var monthEnd =
            monthStart.AddMonths(1);

        var plannedPersonIds =
            members
                .Where(x =>
                    x.FamilyRoleCode == FamilyRoles.Adult &&
                    x.SharePlannedIncome)
                .Select(x => x.PersonId)
                .Distinct()
                .ToArray();

        var actualPersonIds =
            members
                .Where(x =>
                    x.FamilyRoleCode == FamilyRoles.Adult &&
                    x.ShareActualIncome)
                .Select(x => x.PersonId)
                .Distinct()
                .ToArray();

        var plannedIncome =
            plannedPersonIds.Length == 0
                ? new Dictionary<Guid, long>()
                : await dbContext.PersonalRecurringOccurrences
                    .AsNoTracking()
                    .Where(x =>
                        plannedPersonIds.Contains(x.OwnerPersonId) &&
                        x.PlannedDateUtc >= monthStart &&
                        x.PlannedDateUtc < monthEnd &&
                        x.KindCode == PersonalTransactionKinds.Income)
                    .GroupBy(x => x.OwnerPersonId)
                    .Select(x => new
                    {
                        PersonId = x.Key,
                        AmountMinor = x.Sum(y => y.PlannedAmountMinor)
                    })
                    .ToDictionaryAsync(
                        x => x.PersonId,
                        x => x.AmountMinor,
                        cancellationToken);

        var actualIncome =
            actualPersonIds.Length == 0
                ? new Dictionary<Guid, long>()
                : await dbContext.PersonalFinancialTransactions
                    .AsNoTracking()
                    .Where(x =>
                        actualPersonIds.Contains(x.OwnerPersonId) &&
                        x.OccurredAtUtc >= monthStart &&
                        x.OccurredAtUtc < monthEnd &&
                        (x.KindCode == PersonalTransactionKinds.Income ||
                         (x.KindCode == PersonalTransactionKinds.Correction &&
                          x.CorrectsTransactionId.HasValue &&
                          dbContext.PersonalFinancialTransactions.Any(source =>
                              source.Id == x.CorrectsTransactionId.Value &&
                              source.OwnerPersonId == x.OwnerPersonId &&
                              source.KindCode == PersonalTransactionKinds.Income))))
                    .GroupBy(x => x.OwnerPersonId)
                    .Select(x => new
                    {
                        PersonId = x.Key,
                        AmountMinor = x.Sum(y => y.AmountMinor)
                    })
                    .ToDictionaryAsync(
                        x => x.PersonId,
                        x => x.AmountMinor,
                        cancellationToken);

        var childIncomeRows =
            await GetChildIncomeRuleRowsAsync(
                selectedGroup.FamilyGroupId,
                cancellationToken);

        var childPlannedIncome =
            childIncomeRows
                .Where(x => AppliesInMonth(x, monthStart))
                .GroupBy(x => x.BeneficiaryPersonId)
                .ToDictionary(
                    x => x.Key,
                    x => x.Sum(y => y.PlannedAmountMinor));

        var memberNames =
            members.ToDictionary(
                x => x.PersonId,
                x => x.DisplayName);

        var childIncomeRules =
            childIncomeRows
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => x.Name)
                .Select(x =>
                    new FamilyChildIncomeRuleSummary(
                        x.Id,
                        x.BeneficiaryPersonId,
                        memberNames.GetValueOrDefault(
                            x.BeneficiaryPersonId,
                            "Dziecko"),
                        x.IncomeKindCode,
                        FamilyIncomeKinds.GetNamePl(
                            x.IncomeKindCode),
                        x.Name,
                        FamilyFinanceMoney.FromMinorUnits(
                            x.PlannedAmountMinor),
                        x.FrequencyCode,
                        FamilyRecurringFrequencies.GetNamePl(
                            x.FrequencyCode),
                        x.DueDay,
                        x.ActiveFromUtc,
                        x.ActiveToUtc,
                        x.IsActive,
                        AppliesInMonth(x, monthStart)))
                .ToArray();

        var memberSummaries =
            members
                .Select(member =>
                    new FamilyMemberBudgetSummary(
                        member.MembershipId,
                        member.PersonId,
                        member.DisplayName,
                        member.FamilyRoleCode,
                        FamilyRoles.GetNamePl(
                            member.FamilyRoleCode),
                        member.ValidFromUtc,
                        member.SharePlannedIncome,
                        member.ShareActualIncome,
                        member.ShareFamilyExpenses,
                        member.ShareRecurringRules,
                        member.FamilyRoleCode == FamilyRoles.Child
                            ? FamilyFinanceMoney.FromMinorUnits(
                                childPlannedIncome.GetValueOrDefault(
                                    member.PersonId))
                            : member.SharePlannedIncome
                                ? FamilyFinanceMoney.FromMinorUnits(
                                    plannedIncome.GetValueOrDefault(
                                        member.PersonId))
                                : null,
                        member.FamilyRoleCode == FamilyRoles.Child
                            ? null
                            : member.ShareActualIncome
                                ? FamilyFinanceMoney.FromMinorUnits(
                                    actualIncome.GetValueOrDefault(
                                        member.PersonId))
                                : null))
                .ToArray();

        var ownMember =
            members.SingleOrDefault(x =>
                x.PersonId == actor.PersonId);

        FamilySharingSnapshot? ownSharing = null;

        if (ownMember is not null &&
            ownMember.FamilyRoleCode == FamilyRoles.Adult)
        {
            ownSharing =
                new FamilySharingSnapshot(
                    selectedGroup.FamilyGroupId,
                    selectedGroup.Name,
                    actor.PersonId,
                    ownMember.DisplayName,
                    ownMember.SharePlannedIncome,
                    ownMember.ShareActualIncome,
                    ownMember.ShareFamilyExpenses,
                    ownMember.ShareRecurringRules,
                    ownMember.SharingEffectiveFromUtc ??
                        ownMember.ValidFromUtc);
        }

        return new FamilyFinanceOverview(
            actor.HouseholdId,
            actor.HouseholdName,
            actor.PersonId,
            year,
            month,
            groups,
            selectedGroup.FamilyGroupId,
            selectedGroup.Name,
            selectedGroup.CurrentMemberRoleCode,
            memberSummaries,
            memberSummaries
                .Where(x => x.PlannedIncome.HasValue)
                .Sum(x => x.PlannedIncome!.Value),
            memberSummaries
                .Where(x => x.ActualIncome.HasValue)
                .Sum(x => x.ActualIncome!.Value),
            childIncomeRules,
            ownSharing);
    }

    public async Task<Guid> CreateGroupAsync(
        CreateFamilyGroupRequest request,
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

        var actor =
            await GetActorContextAsync(
                actorUserId,
                cancellationToken);

        if (actor.HouseholdId is null)
        {
            throw new InvalidOperationException(
                "Najpierw skonfiguruj gospodarstwo w module Finanse domu.");
        }

        var name =
            NormalizeRequiredText(
                request.Name,
                "Nazwa rodziny",
                160);

        var now = DateTime.UtcNow;
        var groupId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var sharingId = Guid.NewGuid();

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await ExecuteAsync(
            """
            INSERT INTO FamilyGroups
                (Id, HouseholdId, Name, IsActive, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc)
            VALUES
                ($id, $householdId, $name, 1, $actorUserId, $now, $now);
            """,
            [
                P("$id", groupId),
                P("$householdId", actor.HouseholdId.Value),
                P("$name", name),
                P("$actorUserId", actorUserId),
                P("$now", now)
            ],
            cancellationToken);

        await ExecuteAsync(
            """
            INSERT INTO FamilyMembers
                (Id, FamilyGroupId, PersonId, FamilyRoleCode, ValidFromUtc, ValidToUtc, CreatedByUserId, CreatedAtUtc)
            VALUES
                ($id, $groupId, $personId, $roleCode, $now, NULL, $actorUserId, $now);
            """,
            [
                P("$id", membershipId),
                P("$groupId", groupId),
                P("$personId", actor.PersonId),
                P("$roleCode", FamilyRoles.Adult),
                P("$actorUserId", actorUserId),
                P("$now", now)
            ],
            cancellationToken);

        await ExecuteAsync(
            """
            INSERT INTO FamilySharingPolicies
                (Id, FamilyGroupId, PersonId, SharePlannedIncome, ShareActualIncome,
                 ShareFamilyExpenses, ShareRecurringRules, EffectiveFromUtc, EffectiveToUtc,
                 CreatedByUserId, CreatedAtUtc)
            VALUES
                ($id, $groupId, $personId, 0, 0, 0, 0, $now, NULL, $actorUserId, $now);
            """,
            [
                P("$id", sharingId),
                P("$groupId", groupId),
                P("$personId", actor.PersonId),
                P("$actorUserId", actorUserId),
                P("$now", now)
            ],
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.8.1.FamilyGroupCreated",
                EntityType: "FamilyGroup",
                EntityId: groupId.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Utworzono grupę rodzinną i dodano autora jako aktywnego członka Adult. Prywatne kwoty nie są zapisywane w audycie."),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return groupId;
    }

    public async Task<AddFamilyMemberForm?> GetAddMemberFormAsync(
        Guid familyGroupId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            FamilyFinancePermissions.Manage,
            cancellationToken);

        var actor =
            await GetActorContextAsync(
                actorUserId,
                cancellationToken);

        if (actor.HouseholdId is null)
        {
            return null;
        }

        var group =
            await EnsureActiveMembershipAsync(
                familyGroupId,
                actor.HouseholdId.Value,
                actor.PersonId,
                cancellationToken);

        var activePersonIds =
            (await GetActiveMembersAsync(
                familyGroupId,
                cancellationToken))
            .Select(x => x.PersonId)
            .ToHashSet();

        var candidates =
            await dbContext.People
                .AsNoTracking()
                .Where(person =>
                    person.IsActive &&
                    (dbContext.HouseholdMembers.Any(member =>
                         member.PersonId == person.Id &&
                         member.HouseholdId == actor.HouseholdId.Value &&
                         member.IsActive) ||
                     !dbContext.HouseholdMembers.Any(member =>
                         member.PersonId == person.Id &&
                         member.IsActive)))
                .OrderBy(person => person.FirstName)
                .ThenBy(person => person.LastName)
                .Select(person => new
                {
                    person.Id,
                    person.DisplayName,
                    person.FirstName,
                    person.LastName
                })
                .ToArrayAsync(cancellationToken);

        return new AddFamilyMemberForm(
            familyGroupId,
            group.Name,
            candidates
                .Where(x => !activePersonIds.Contains(x.Id))
                .Select(x =>
                    new FamilyMemberCandidate(
                        x.Id,
                        BuildDisplayName(
                            x.DisplayName,
                            x.FirstName,
                            x.LastName)))
                .ToArray());
    }

    public async Task<Guid> AddMemberAsync(
        AddFamilyMemberRequest request,
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

        if (!FamilyRoles.IsValid(request.FamilyRoleCode))
        {
            throw new ArgumentException(
                "Wybierz poprawną rolę członka rodziny.");
        }

        var actor =
            await GetActorContextAsync(
                actorUserId,
                cancellationToken);

        if (actor.HouseholdId is null)
        {
            throw new InvalidOperationException(
                "Brak aktywnego gospodarstwa.");
        }

        await EnsureActiveMembershipAsync(
            request.FamilyGroupId,
            actor.HouseholdId.Value,
            actor.PersonId,
            cancellationToken);

        var selectedPersonExists =
            await dbContext.People
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.Id == request.PersonId &&
                        x.IsActive,
                    cancellationToken);

        if (!selectedPersonExists)
        {
            throw new InvalidOperationException(
                "Wybrana osoba nie istnieje albo jest nieaktywna.");
        }

        var activeHouseholdsForPerson =
            await dbContext.HouseholdMembers
                .AsNoTracking()
                .Where(x =>
                    x.PersonId == request.PersonId &&
                    x.IsActive)
                .Select(x => x.HouseholdId)
                .Distinct()
                .ToArrayAsync(cancellationToken);

        if (activeHouseholdsForPerson.Length > 0 &&
            !activeHouseholdsForPerson.Contains(actor.HouseholdId.Value))
        {
            throw new InvalidOperationException(
                "Wybrana osoba należy do innego aktywnego gospodarstwa.");
        }

        var alreadyActive =
            await ScalarLongAsync(
                """
                SELECT COUNT(1)
                FROM FamilyMembers
                WHERE FamilyGroupId = $groupId
                  AND PersonId = $personId
                  AND ValidToUtc IS NULL;
                """,
                [
                    P("$groupId", request.FamilyGroupId),
                    P("$personId", request.PersonId)
                ],
                cancellationToken) > 0;

        if (alreadyActive)
        {
            throw new InvalidOperationException(
                "Ta osoba jest już aktywnym członkiem rodziny.");
        }

        var membershipId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await ExecuteAsync(
            """
            INSERT INTO FamilyMembers
                (Id, FamilyGroupId, PersonId, FamilyRoleCode, ValidFromUtc, ValidToUtc, CreatedByUserId, CreatedAtUtc)
            VALUES
                ($id, $groupId, $personId, $roleCode, $now, NULL, $actorUserId, $now);
            """,
            [
                P("$id", membershipId),
                P("$groupId", request.FamilyGroupId),
                P("$personId", request.PersonId),
                P("$roleCode", request.FamilyRoleCode),
                P("$actorUserId", actorUserId),
                P("$now", now)
            ],
            cancellationToken);

        if (request.FamilyRoleCode == FamilyRoles.Adult)
        {
            await ExecuteAsync(
                """
                INSERT INTO FamilySharingPolicies
                    (Id, FamilyGroupId, PersonId, SharePlannedIncome, ShareActualIncome,
                     ShareFamilyExpenses, ShareRecurringRules, EffectiveFromUtc, EffectiveToUtc,
                     CreatedByUserId, CreatedAtUtc)
                VALUES
                    ($id, $groupId, $personId, 0, 0, 0, 0, $now, NULL, $actorUserId, $now);
                """,
                [
                    P("$id", Guid.NewGuid()),
                    P("$groupId", request.FamilyGroupId),
                    P("$personId", request.PersonId),
                    P("$actorUserId", actorUserId),
                    P("$now", now)
                ],
                cancellationToken);
        }

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.8.1.FamilyMemberAdded",
                EntityType: "FamilyMember",
                EntityId: membershipId.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    $"Dodano członka rodziny w roli {FamilyRoles.GetNamePl(request.FamilyRoleCode)}."),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return membershipId;
    }

    public async Task EndMembershipAsync(
        Guid membershipId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            FamilyFinancePermissions.Manage,
            cancellationToken);

        var actor =
            await GetActorContextAsync(
                actorUserId,
                cancellationToken);

        if (actor.HouseholdId is null)
        {
            throw new InvalidOperationException(
                "Brak aktywnego gospodarstwa.");
        }

        var target =
            await GetMembershipByIdAsync(
                membershipId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Nie znaleziono aktywnego członkostwa.");

        await EnsureActiveMembershipAsync(
            target.FamilyGroupId,
            actor.HouseholdId.Value,
            actor.PersonId,
            cancellationToken);

        if (target.PersonId == actor.PersonId)
        {
            throw new InvalidOperationException(
                "Nie można zakończyć własnego członkostwa z tego ekranu. Najpierw dodaj innego dorosłego zarządzającego rodziną.");
        }

        var now = DateTime.UtcNow;

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var changed =
            await ExecuteAsync(
                """
                UPDATE FamilyMembers
                SET ValidToUtc = $now
                WHERE Id = $id
                  AND ValidToUtc IS NULL;
                """,
                [
                    P("$now", now),
                    P("$id", membershipId)
                ],
                cancellationToken);

        if (changed == 0)
        {
            throw new InvalidOperationException(
                "Członkostwo zostało już zakończone.");
        }

        await ExecuteAsync(
            """
            UPDATE FamilySharingPolicies
            SET EffectiveToUtc = $now
            WHERE FamilyGroupId = $groupId
              AND PersonId = $personId
              AND EffectiveToUtc IS NULL;
            """,
            [
                P("$now", now),
                P("$groupId", target.FamilyGroupId),
                P("$personId", target.PersonId)
            ],
            cancellationToken);

        await ExecuteAsync(
            """
            UPDATE FamilyRecurringRules
            SET IsActive = 0,
                ActiveToUtc = CASE
                    WHEN ActiveToUtc IS NULL OR ActiveToUtc > $now THEN $now
                    ELSE ActiveToUtc
                END,
                UpdatedAtUtc = $now
            WHERE FamilyGroupId = $groupId
              AND BeneficiaryPersonId = $personId
              AND RuleTypeCode = $typeCode
              AND IsActive = 1;
            """,
            [
                P("$now", now),
                P("$groupId", target.FamilyGroupId),
                P("$personId", target.PersonId),
                P("$typeCode", FamilyRecurringRuleTypes.Income)
            ],
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.8.1.FamilyMembershipEnded",
                EntityType: "FamilyMember",
                EntityId: membershipId.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Zakończono członkostwo w grupie rodzinnej. Rekord historyczny pozostaje zachowany."),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);
    }

    public async Task<FamilySharingSnapshot?> GetOwnSharingAsync(
        Guid familyGroupId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            FamilyFinancePermissions.ShareOwn,
            cancellationToken);

        var actor =
            await GetActorContextAsync(
                actorUserId,
                cancellationToken);

        if (actor.HouseholdId is null)
        {
            return null;
        }

        var group =
            await EnsureActiveMembershipAsync(
                familyGroupId,
                actor.HouseholdId.Value,
                actor.PersonId,
                cancellationToken);

        var members =
            await GetActiveMembersAsync(
                familyGroupId,
                cancellationToken);

        var own =
            members.SingleOrDefault(x =>
                x.PersonId == actor.PersonId);

        if (own is null ||
            own.FamilyRoleCode != FamilyRoles.Adult)
        {
            throw new UnauthorizedAccessException(
                "Ustawienia udostępniania są dostępne wyłącznie dla dorosłego członka rodziny.");
        }

        return new FamilySharingSnapshot(
            familyGroupId,
            group.Name,
            actor.PersonId,
            own.DisplayName,
            own.SharePlannedIncome,
            own.ShareActualIncome,
            own.ShareFamilyExpenses,
            own.ShareRecurringRules,
            own.SharingEffectiveFromUtc ?? own.ValidFromUtc);
    }

    public async Task UpdateOwnSharingAsync(
        UpdateFamilySharingRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            FamilyFinancePermissions.ShareOwn,
            cancellationToken);

        var actor =
            await GetActorContextAsync(
                actorUserId,
                cancellationToken);

        if (actor.HouseholdId is null)
        {
            throw new InvalidOperationException(
                "Brak aktywnego gospodarstwa.");
        }

        await EnsureActiveMembershipAsync(
            request.FamilyGroupId,
            actor.HouseholdId.Value,
            actor.PersonId,
            cancellationToken);

        var ownMember =
            (await GetActiveMembersAsync(
                request.FamilyGroupId,
                cancellationToken))
            .Single(x => x.PersonId == actor.PersonId);

        if (ownMember.FamilyRoleCode != FamilyRoles.Adult)
        {
            throw new UnauthorizedAccessException(
                "Dziecko nie udostępnia prywatnych finansów do budżetu rodzinnego.");
        }

        var now = DateTime.UtcNow;

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await ExecuteAsync(
            """
            UPDATE FamilySharingPolicies
            SET EffectiveToUtc = $now
            WHERE FamilyGroupId = $groupId
              AND PersonId = $personId
              AND EffectiveToUtc IS NULL;
            """,
            [
                P("$now", now),
                P("$groupId", request.FamilyGroupId),
                P("$personId", actor.PersonId)
            ],
            cancellationToken);

        await ExecuteAsync(
            """
            INSERT INTO FamilySharingPolicies
                (Id, FamilyGroupId, PersonId, SharePlannedIncome, ShareActualIncome,
                 ShareFamilyExpenses, ShareRecurringRules, EffectiveFromUtc, EffectiveToUtc,
                 CreatedByUserId, CreatedAtUtc)
            VALUES
                ($id, $groupId, $personId, $planned, $actual, $expenses, $rules,
                 $now, NULL, $actorUserId, $now);
            """,
            [
                P("$id", Guid.NewGuid()),
                P("$groupId", request.FamilyGroupId),
                P("$personId", actor.PersonId),
                P("$planned", request.SharePlannedIncome),
                P("$actual", request.ShareActualIncome),
                P("$expenses", request.ShareFamilyExpenses),
                P("$rules", request.ShareRecurringRules),
                P("$actorUserId", actorUserId),
                P("$now", now)
            ],
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.8.1.FamilySharingChanged",
                EntityType: "FamilySharingPolicy",
                EntityId: request.FamilyGroupId.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Zmieniono własny zakres udostępniania danych do budżetu rodzinnego. Audyt nie zapisuje prywatnych kwot."),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);
    }

    public async Task<Guid> CreateChildIncomeAsync(
        CreateFamilyChildIncomeRequest request,
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

        if (!FamilyIncomeKinds.IsValid(request.IncomeKindCode))
        {
            throw new ArgumentException(
                "Wybierz poprawny rodzaj przychodu dziecka.");
        }

        if (!FamilyRecurringFrequencies.IsValid(request.FrequencyCode))
        {
            throw new ArgumentException(
                "Wybierz poprawną częstotliwość przychodu.");
        }

        if (request.DueDay < 1 || request.DueDay > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.DueDay),
                "Dzień wpływu musi mieścić się w zakresie 1-31.");
        }

        var actor =
            await GetActorContextAsync(
                actorUserId,
                cancellationToken);

        if (actor.HouseholdId is null)
        {
            throw new InvalidOperationException(
                "Brak aktywnego gospodarstwa.");
        }

        await EnsureActiveMembershipAsync(
            request.FamilyGroupId,
            actor.HouseholdId.Value,
            actor.PersonId,
            cancellationToken);

        var child =
            (await GetActiveMembersAsync(
                request.FamilyGroupId,
                cancellationToken))
            .SingleOrDefault(x =>
                x.PersonId == request.BeneficiaryPersonId &&
                x.FamilyRoleCode == FamilyRoles.Child)
            ?? throw new InvalidOperationException(
                "Wybrana osoba nie jest aktywnym dzieckiem w tej rodzinie.");

        var amountMinor =
            FamilyFinanceMoney.ToMinorUnits(
                request.PlannedAmount);

        var activeFrom =
            DateTime.SpecifyKind(
                request.ActiveFromUtc.Date,
                DateTimeKind.Utc);

        DateTime? activeTo =
            request.ActiveToUtc.HasValue
                ? DateTime.SpecifyKind(
                    request.ActiveToUtc.Value.Date,
                    DateTimeKind.Utc)
                : null;

        if (request.FrequencyCode == FamilyRecurringFrequencies.Once)
        {
            activeTo = activeFrom;
        }

        if (activeTo.HasValue && activeTo.Value < activeFrom)
        {
            throw new ArgumentException(
                "Data końca nie może być wcześniejsza od daty początku.");
        }

        var name =
            request.IncomeKindCode == FamilyIncomeKinds.Other
                ? NormalizeRequiredText(
                    request.CustomName,
                    "Nazwa przychodu",
                    160)
                : FamilyIncomeKinds.GetNamePl(
                    request.IncomeKindCode);

        var now = DateTime.UtcNow;
        var ruleId = Guid.NewGuid();

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await ExecuteAsync(
            """
            INSERT INTO FamilyRecurringRules
                (Id, FamilyGroupId, Name, RuleTypeCode, CategoryCode,
                 PlannedAmountMinor, FrequencyCode, DueDay, BeneficiaryPersonId,
                 ActiveFromUtc, ActiveToUtc, IsActive,
                 CreatedByUserId, CreatedAtUtc, UpdatedAtUtc)
            VALUES
                ($id, $groupId, $name, $typeCode, $incomeKindCode,
                 $amountMinor, $frequencyCode, $dueDay, $beneficiaryPersonId,
                 $activeFromUtc, $activeToUtc, 1,
                 $actorUserId, $now, $now);
            """,
            [
                P("$id", ruleId),
                P("$groupId", request.FamilyGroupId),
                P("$name", name),
                P("$typeCode", FamilyRecurringRuleTypes.Income),
                P("$incomeKindCode", request.IncomeKindCode),
                P("$amountMinor", amountMinor),
                P("$frequencyCode", request.FrequencyCode),
                P("$dueDay", request.DueDay),
                P("$beneficiaryPersonId", child.PersonId),
                P("$activeFromUtc", activeFrom),
                P("$activeToUtc", activeTo),
                P("$actorUserId", actorUserId),
                P("$now", now)
            ],
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.8.4.ChildIncomeCreated",
                EntityType: "FamilyRecurringRule",
                EntityId: ruleId.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Dodano planowany przychód przypisany do dziecka. Kwota nie jest zapisywana w audycie i nie tworzy salda na koncie dziecka."),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return ruleId;
    }

    public async Task DeactivateChildIncomeAsync(
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

        var actor =
            await GetActorContextAsync(
                actorUserId,
                cancellationToken);

        if (actor.HouseholdId is null)
        {
            throw new InvalidOperationException(
                "Brak aktywnego gospodarstwa.");
        }

        var rule =
            await GetChildIncomeRuleByIdAsync(
                ruleId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Nie znaleziono przychodu dziecka.");

        await EnsureActiveMembershipAsync(
            rule.FamilyGroupId,
            actor.HouseholdId.Value,
            actor.PersonId,
            cancellationToken);

        if (!rule.IsActive)
        {
            return;
        }

        var now = DateTime.UtcNow;

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await ExecuteAsync(
            """
            UPDATE FamilyRecurringRules
            SET IsActive = 0,
                ActiveToUtc = CASE
                    WHEN ActiveToUtc IS NULL OR ActiveToUtc > $now THEN $now
                    ELSE ActiveToUtc
                END,
                UpdatedAtUtc = $now
            WHERE Id = $id
              AND RuleTypeCode = $typeCode
              AND IsActive = 1;
            """,
            [
                P("$now", now),
                P("$id", ruleId),
                P("$typeCode", FamilyRecurringRuleTypes.Income)
            ],
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.8.4.ChildIncomeDeactivated",
                EntityType: "FamilyRecurringRule",
                EntityId: ruleId.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Zakończono planowany przychód przypisany do dziecka. Historia reguły pozostaje zachowana."),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<ActorContext> GetActorContextAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var row =
            await (
                from account in dbContext.UserAccounts.AsNoTracking()
                join person in dbContext.People.AsNoTracking()
                    on account.PersonId equals person.Id
                where
                    account.Id == actorUserId &&
                    account.IsActive &&
                    person.IsActive
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

        var household =
            await (
                from member in dbContext.HouseholdMembers.AsNoTracking()
                join item in dbContext.Households.AsNoTracking()
                    on member.HouseholdId equals item.Id
                where
                    member.PersonId == row.Id &&
                    member.IsActive &&
                    item.IsActive
                select new
                {
                    item.Id,
                    item.Name
                })
                .FirstOrDefaultAsync(cancellationToken);

        return new ActorContext(
            row.Id,
            BuildDisplayName(
                row.DisplayName,
                row.FirstName,
                row.LastName),
            household?.Id,
            household?.Name);
    }

    private async Task<IReadOnlyList<FamilyGroupChoice>> GetAccessibleGroupsAsync(
        Guid householdId,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var result = new List<FamilyGroupChoice>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT g.Id, g.Name, m.FamilyRoleCode
                    FROM FamilyGroups g
                    INNER JOIN FamilyMembers m
                        ON m.FamilyGroupId = g.Id
                    WHERE g.HouseholdId = $householdId
                      AND g.IsActive = 1
                      AND m.PersonId = $personId
                      AND m.ValidToUtc IS NULL
                    ORDER BY g.Name;
                    """;

                AddParameter(command, "$householdId", householdId);
                AddParameter(command, "$personId", personId);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    var role = reader.GetString(2);
                    result.Add(
                        new FamilyGroupChoice(
                            reader.GetGuid(0),
                            reader.GetString(1),
                            role,
                            FamilyRoles.GetNamePl(role)));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<IReadOnlyList<FamilyMemberRow>> GetActiveMembersAsync(
        Guid familyGroupId,
        CancellationToken cancellationToken)
    {
        var result = new List<FamilyMemberRow>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT
                        m.Id,
                        m.PersonId,
                        p.DisplayName,
                        p.FirstName,
                        p.LastName,
                        m.FamilyRoleCode,
                        m.ValidFromUtc,
                        COALESCE(s.SharePlannedIncome, 0),
                        COALESCE(s.ShareActualIncome, 0),
                        COALESCE(s.ShareFamilyExpenses, 0),
                        COALESCE(s.ShareRecurringRules, 0),
                        s.EffectiveFromUtc
                    FROM FamilyMembers m
                    INNER JOIN People p
                        ON p.Id = m.PersonId
                    LEFT JOIN FamilySharingPolicies s
                        ON s.FamilyGroupId = m.FamilyGroupId
                       AND s.PersonId = m.PersonId
                       AND s.EffectiveToUtc IS NULL
                    WHERE m.FamilyGroupId = $groupId
                      AND m.ValidToUtc IS NULL
                      AND p.IsActive = 1
                    ORDER BY
                        CASE WHEN m.FamilyRoleCode = 'Adult' THEN 0 ELSE 1 END,
                        p.FirstName,
                        p.LastName;
                    """;

                AddParameter(command, "$groupId", familyGroupId);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    result.Add(
                        new FamilyMemberRow(
                            reader.GetGuid(0),
                            reader.GetGuid(1),
                            BuildDisplayName(
                                reader.IsDBNull(2) ? null : reader.GetString(2),
                                reader.GetString(3),
                                reader.GetString(4)),
                            reader.GetString(5),
                            ReadDateTime(reader, 6),
                            reader.GetInt64(7) != 0,
                            reader.GetInt64(8) != 0,
                            reader.GetInt64(9) != 0,
                            reader.GetInt64(10) != 0,
                            reader.IsDBNull(11)
                                ? null
                                : ReadDateTime(reader, 11)));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<GroupRow> EnsureActiveMembershipAsync(
        Guid familyGroupId,
        Guid householdId,
        Guid personId,
        CancellationToken cancellationToken)
    {
        GroupRow? result = null;

        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT g.Id, g.Name
                    FROM FamilyGroups g
                    INNER JOIN FamilyMembers m
                        ON m.FamilyGroupId = g.Id
                    WHERE g.Id = $groupId
                      AND g.HouseholdId = $householdId
                      AND g.IsActive = 1
                      AND m.PersonId = $personId
                      AND m.ValidToUtc IS NULL
                    LIMIT 1;
                    """;

                AddParameter(command, "$groupId", familyGroupId);
                AddParameter(command, "$householdId", householdId);
                AddParameter(command, "$personId", personId);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                if (await reader.ReadAsync(cancellationToken))
                {
                    result =
                        new GroupRow(
                            reader.GetGuid(0),
                            reader.GetString(1));
                }
            },
            cancellationToken);

        return result
            ?? throw new UnauthorizedAccessException(
                "Dostęp do rodziny wymaga aktywnego członkostwa w tej grupie.");
    }

    private async Task<MembershipRow?> GetMembershipByIdAsync(
        Guid membershipId,
        CancellationToken cancellationToken)
    {
        MembershipRow? result = null;

        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT Id, FamilyGroupId, PersonId, FamilyRoleCode
                    FROM FamilyMembers
                    WHERE Id = $id
                      AND ValidToUtc IS NULL
                    LIMIT 1;
                    """;

                AddParameter(command, "$id", membershipId);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                if (await reader.ReadAsync(cancellationToken))
                {
                    result =
                        new MembershipRow(
                            reader.GetGuid(0),
                            reader.GetGuid(1),
                            reader.GetGuid(2),
                            reader.GetString(3));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<IReadOnlyList<ChildIncomeRuleRow>> GetChildIncomeRuleRowsAsync(
        Guid familyGroupId,
        CancellationToken cancellationToken)
    {
        var result = new List<ChildIncomeRuleRow>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT Id, FamilyGroupId, CategoryCode, Name,
                           PlannedAmountMinor, FrequencyCode, DueDay,
                           BeneficiaryPersonId, ActiveFromUtc, ActiveToUtc,
                           IsActive
                    FROM FamilyRecurringRules
                    WHERE FamilyGroupId = $groupId
                      AND RuleTypeCode = $typeCode
                      AND BeneficiaryPersonId IS NOT NULL
                    ORDER BY Name;
                    """;

                AddParameter(command, "$groupId", familyGroupId);
                AddParameter(command, "$typeCode", FamilyRecurringRuleTypes.Income);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    result.Add(
                        new ChildIncomeRuleRow(
                            reader.GetGuid(0),
                            reader.GetGuid(1),
                            reader.GetString(2),
                            reader.GetString(3),
                            reader.GetInt64(4),
                            reader.GetString(5),
                            reader.GetInt32(6),
                            reader.GetGuid(7),
                            ReadDateTime(reader, 8),
                            reader.IsDBNull(9)
                                ? (DateTime?)null
                                : ReadDateTime(reader, 9),
                            reader.GetInt64(10) != 0));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<ChildIncomeRuleRow?> GetChildIncomeRuleByIdAsync(
        Guid ruleId,
        CancellationToken cancellationToken)
    {
        ChildIncomeRuleRow? result = null;

        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT Id, FamilyGroupId, CategoryCode, Name,
                           PlannedAmountMinor, FrequencyCode, DueDay,
                           BeneficiaryPersonId, ActiveFromUtc, ActiveToUtc,
                           IsActive
                    FROM FamilyRecurringRules
                    WHERE Id = $id
                      AND RuleTypeCode = $typeCode
                      AND BeneficiaryPersonId IS NOT NULL
                    LIMIT 1;
                    """;

                AddParameter(command, "$id", ruleId);
                AddParameter(command, "$typeCode", FamilyRecurringRuleTypes.Income);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                if (await reader.ReadAsync(cancellationToken))
                {
                    result = new ChildIncomeRuleRow(
                        reader.GetGuid(0),
                        reader.GetGuid(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetInt64(4),
                        reader.GetString(5),
                        reader.GetInt32(6),
                        reader.GetGuid(7),
                        ReadDateTime(reader, 8),
                        reader.IsDBNull(9)
                            ? (DateTime?)null
                            : ReadDateTime(reader, 9),
                        reader.GetInt64(10) != 0);
                }
            },
            cancellationToken);

        return result;
    }

    private static bool AppliesInMonth(
        ChildIncomeRuleRow rule,
        DateTime monthStart)
    {
        var startMonth =
            new DateTime(
                rule.ActiveFromUtc.Year,
                rule.ActiveFromUtc.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        if (monthStart < startMonth)
        {
            return false;
        }

        if (rule.ActiveToUtc.HasValue)
        {
            var endMonth =
                new DateTime(
                    rule.ActiveToUtc.Value.Year,
                    rule.ActiveToUtc.Value.Month,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc);

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
            FamilyRecurringFrequencies.Every2Months => months % 2 == 0,
            FamilyRecurringFrequencies.Quarterly => months % 3 == 0,
            FamilyRecurringFrequencies.Every4Months => months % 4 == 0,
            FamilyRecurringFrequencies.SemiAnnual => months % 6 == 0,
            FamilyRecurringFrequencies.Yearly => months % 12 == 0,
            _ => false
        };
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
                    AddParameter(
                        command,
                        parameter.Name,
                        parameter.Value);
                }

                AttachCurrentTransaction(command);

                var value =
                    await command.ExecuteScalarAsync(
                        cancellationToken);

                result = Convert.ToInt64(value ?? 0);
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

    private void AttachCurrentTransaction(
        DbCommand command)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            command.Transaction =
                dbContext.Database.CurrentTransaction
                    .GetDbTransaction();
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

    private static string BuildDisplayName(
        string? displayName,
        string firstName,
        string lastName) =>
        string.IsNullOrWhiteSpace(displayName)
            ? $"{firstName} {lastName}".Trim()
            : displayName.Trim();

    private static void ValidatePeriod(
        int year,
        int month)
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

    private sealed record ActorContext(
        Guid PersonId,
        string DisplayName,
        Guid? HouseholdId,
        string? HouseholdName);

    private sealed record GroupRow(
        Guid Id,
        string Name);

    private sealed record MembershipRow(
        Guid Id,
        Guid FamilyGroupId,
        Guid PersonId,
        string FamilyRoleCode);

    private sealed record FamilyMemberRow(
        Guid MembershipId,
        Guid PersonId,
        string DisplayName,
        string FamilyRoleCode,
        DateTime ValidFromUtc,
        bool SharePlannedIncome,
        bool ShareActualIncome,
        bool ShareFamilyExpenses,
        bool ShareRecurringRules,
        DateTime? SharingEffectiveFromUtc);

    private sealed record ChildIncomeRuleRow(
        Guid Id,
        Guid FamilyGroupId,
        string IncomeKindCode,
        string Name,
        long PlannedAmountMinor,
        string FrequencyCode,
        int DueDay,
        Guid BeneficiaryPersonId,
        DateTime ActiveFromUtc,
        DateTime? ActiveToUtc,
        bool IsActive);

    private sealed record ParameterValue(
        string Name,
        object? Value);
}
