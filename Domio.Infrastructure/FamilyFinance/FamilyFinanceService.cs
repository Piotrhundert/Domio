using System.Data;
using System.Data.Common;
using Domio.Application.Auditing;
using Domio.Application.FamilyFinance;
using Domio.Domain.FamilyFinance;
using Domio.Domain.HouseholdFinance;
using Domio.Domain.PersonalFinance;
using Domio.Domain.Users;
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

        var childIncomeRows =
            await GetChildIncomeRuleRowsAsync(
                selectedGroup.FamilyGroupId,
                cancellationToken);

        var sharedAccountRows =
            await GetSharedAccountRowsForGroupAsync(
                selectedGroup.FamilyGroupId,
                cancellationToken);

        var sharedPersonalAccountIds =
            sharedAccountRows
                .Select(x => x.PersonalAccountId)
                .Distinct()
                .ToArray();

        var selectedPeriodKey =
            $"{year:D4}-{month:D2}";

        var childReceiptsForPeriod =
            await GetChildIncomeReceiptRowsByPeriodAsync(
                selectedGroup.FamilyGroupId,
                selectedPeriodKey,
                cancellationToken);

        var childReceiptsReceivedInMonth =
            await GetChildIncomeReceiptRowsByReceivedRangeAsync(
                selectedGroup.FamilyGroupId,
                monthStart,
                monthEnd,
                cancellationToken);

        var personalChildReceiptSourceIds =
            childReceiptsReceivedInMonth
                .Where(x =>
                    x.SourceType ==
                        FamilyBudgetSourceTypes.PersonalTransaction)
                .Select(x => x.SourceId)
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
                        !sharedPersonalAccountIds.Contains(x.AccountId) &&
                        x.OccurredAtUtc >= monthStart &&
                        x.OccurredAtUtc < monthEnd &&
                        ((x.KindCode == PersonalTransactionKinds.Income &&
                          !personalChildReceiptSourceIds.Contains(x.Id)) ||
                         (x.KindCode == PersonalTransactionKinds.Correction &&
                          x.CorrectsTransactionId.HasValue &&
                          !personalChildReceiptSourceIds.Contains(
                              x.CorrectsTransactionId.Value) &&
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

        var childPlannedIncome =
            childIncomeRows
                .Where(x => AppliesInMonth(x, monthStart))
                .GroupBy(x => x.BeneficiaryPersonId)
                .ToDictionary(
                    x => x.Key,
                    x => x.Sum(y => y.PlannedAmountMinor));

        var childActualIncome =
            childReceiptsReceivedInMonth
                .GroupBy(x => x.BeneficiaryPersonId)
                .ToDictionary(
                    x => x.Key,
                    x => x.Sum(y => y.AmountMinor));

        var receiptsByRule =
            childReceiptsForPeriod
                .GroupBy(x => x.RuleId)
                .ToDictionary(
                    x => x.Key,
                    x => x.Single());

        var memberNames =
            members.ToDictionary(
                x => x.PersonId,
                x => x.DisplayName);

        var currentMonthStart =
            new DateTime(
                DateTime.UtcNow.Year,
                DateTime.UtcNow.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var childIncomeRules =
            childIncomeRows
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => x.Name)
                .Select(x =>
                {
                    var applies = AppliesInMonth(x, monthStart);
                    receiptsByRule.TryGetValue(x.Id, out var receipt);
                    var plannedDate = applies
                        ? BuildPlannedDate(year, month, x.DueDay)
                        : (DateTime?)null;

                    return new FamilyChildIncomeRuleSummary(
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
                        applies,
                        selectedPeriodKey,
                        plannedDate,
                        receipt is not null,
                        receipt is null
                            ? null
                            : FamilyFinanceMoney.FromMinorUnits(
                                receipt.AmountMinor),
                        receipt?.ReceivedAtUtc,
                        applies &&
                            receipt is null &&
                            monthStart <= currentMonthStart);
                })
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
                            ? FamilyFinanceMoney.FromMinorUnits(
                                childActualIncome.GetValueOrDefault(
                                    member.PersonId))
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

        var sharedAccounts =
            await BuildSharedAccountSummariesAsync(
                sharedAccountRows,
                actor.PersonId,
                cancellationToken);

        var childHouseholdContributions =
            await BuildChildHouseholdContributionSummariesAsync(
                actor.HouseholdId.Value,
                members,
                year,
                month,
                cancellationToken);

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
            ownSharing)
        {
            SharedAccounts = sharedAccounts,
            ChildHouseholdContributions = childHouseholdContributions
        };
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

    public async Task<FamilyChildIncomeReceiptForm?> GetChildIncomeReceiptFormAsync(
        Guid familyGroupId,
        Guid ruleId,
        int year,
        int month,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            FamilyFinancePermissions.Manage,
            cancellationToken);

        ValidatePeriod(year, month);

        var actor = await GetActorContextAsync(
            actorUserId,
            cancellationToken);

        if (actor.HouseholdId is null)
        {
            throw new InvalidOperationException(
                "Brak aktywnego gospodarstwa.");
        }

        var familyGroup = await EnsureActiveMembershipAsync(
            familyGroupId,
            actor.HouseholdId.Value,
            actor.PersonId,
            cancellationToken);

        var rule = await GetChildIncomeRuleByIdAsync(
            ruleId,
            cancellationToken);

        if (rule is null || rule.FamilyGroupId != familyGroupId)
        {
            return null;
        }

        var monthStart = new DateTime(
            year,
            month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        if (!AppliesInMonth(rule, monthStart))
        {
            throw new InvalidOperationException(
                "Ten przychód nie przypada w wybranym miesiącu.");
        }

        var currentMonthStart = new DateTime(
            DateTime.UtcNow.Year,
            DateTime.UtcNow.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        if (monthStart > currentMonthStart)
        {
            throw new InvalidOperationException(
                "Nie można potwierdzić wpływu dla przyszłego miesiąca.");
        }

        var periodKey = $"{year:D4}-{month:D2}";
        var existingReceipt = await GetChildIncomeReceiptByRulePeriodAsync(
            ruleId,
            periodKey,
            cancellationToken);

        if (existingReceipt is not null)
        {
            throw new InvalidOperationException(
                "Wpływ tego przychodu w wybranym miesiącu został już potwierdzony.");
        }

        var child = (await GetActiveMembersAsync(
                familyGroupId,
                cancellationToken))
            .SingleOrDefault(x =>
                x.PersonId == rule.BeneficiaryPersonId &&
                x.FamilyRoleCode == FamilyRoles.Child)
            ?? throw new InvalidOperationException(
                "Dziecko przypisane do tego przychodu nie jest już aktywnym członkiem rodziny.");

        var accounts = new List<FamilyIncomeReceiptAccount>();

        var canUsePersonalAccount = await PermissionEnforcement.HasUserAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinancePersonalManageOwn,
            cancellationToken);

        if (canUsePersonalAccount)
        {
            var allSharedRows = await GetSharedAccountRowsForPersonAsync(
                actor.PersonId,
                cancellationToken);

            var sharedPersonalAccountIds = allSharedRows
                .Select(x => x.PersonalAccountId)
                .Distinct()
                .ToArray();

            var personalAccounts = await dbContext.PersonalFinancialAccounts
                .AsNoTracking()
                .Where(x =>
                    x.OwnerPersonId == actor.PersonId &&
                    x.IsActive &&
                    x.CurrencyCode == "PLN" &&
                    !sharedPersonalAccountIds.Contains(x.Id))
                .OrderBy(x => x.Name)
                .ToArrayAsync(cancellationToken);

            var accountIds = personalAccounts.Select(x => x.Id).ToArray();
            var balances = accountIds.Length == 0
                ? new Dictionary<Guid, long>()
                : await dbContext.PersonalFinancialTransactions
                    .AsNoTracking()
                    .Where(x =>
                        x.OwnerPersonId == actor.PersonId &&
                        accountIds.Contains(x.AccountId))
                    .GroupBy(x => x.AccountId)
                    .Select(x => new
                    {
                        AccountId = x.Key,
                        BalanceMinor = x.Sum(y => y.AmountMinor)
                    })
                    .ToDictionaryAsync(
                        x => x.AccountId,
                        x => x.BalanceMinor,
                        cancellationToken);

            accounts.AddRange(
                personalAccounts.Select(account =>
                    new FamilyIncomeReceiptAccount(
                        FamilyIncomeReceiptAccountTypes.PersonalAccount,
                        FamilyIncomeReceiptAccountTypes.GetNamePl(
                            FamilyIncomeReceiptAccountTypes.PersonalAccount),
                        account.Id,
                        account.Name,
                        account.CurrencyCode,
                        FamilyFinanceMoney.FromMinorUnits(
                            balances.GetValueOrDefault(account.Id)))));

            var sharedRows = allSharedRows
                .Where(x => x.FamilyGroupId == familyGroupId)
                .ToArray();

            var sharedAccounts = await BuildSharedAccountSummariesAsync(
                sharedRows,
                actor.PersonId,
                cancellationToken);

            accounts.AddRange(
                sharedAccounts
                    .Where(x => x.IsActive && x.CurrencyCode == "PLN")
                    .Select(account =>
                        new FamilyIncomeReceiptAccount(
                            FamilyIncomeReceiptAccountTypes.FamilySharedAccount,
                            FamilyIncomeReceiptAccountTypes.GetNamePl(
                                FamilyIncomeReceiptAccountTypes.FamilySharedAccount),
                            account.AccountId,
                            $"{account.AccountName} ({account.OwnerDisplayName} + {account.CoOwnerDisplayName})",
                            account.CurrencyCode,
                            account.Balance)));
        }

        var canManageHousehold = await PermissionEnforcement.HasUserAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinanceHouseholdManage,
            cancellationToken);

        // Konto domu pobieramy po gospodarstwie grupy, nie po identyfikatorze rodziny.
        if (canManageHousehold)
        {
            var householdAccounts = await dbContext.HouseholdAccounts
                .AsNoTracking()
                .Where(x =>
                    x.HouseholdId == actor.HouseholdId.Value &&
                    x.IsActive &&
                    x.CurrencyCode == "PLN")
                .OrderBy(x => x.Name)
                .ToArrayAsync(cancellationToken);

            var accountIds = householdAccounts.Select(x => x.Id).ToArray();
            var balances = accountIds.Length == 0
                ? new Dictionary<Guid, long>()
                : await dbContext.HouseholdEntries
                    .AsNoTracking()
                    .Where(x =>
                        x.HouseholdId == actor.HouseholdId.Value &&
                        accountIds.Contains(x.AccountId))
                    .GroupBy(x => x.AccountId)
                    .Select(x => new
                    {
                        AccountId = x.Key,
                        BalanceMinor = x.Sum(y => y.AmountMinor)
                    })
                    .ToDictionaryAsync(
                        x => x.AccountId,
                        x => x.BalanceMinor,
                        cancellationToken);

            accounts.AddRange(
                householdAccounts.Select(account =>
                    new FamilyIncomeReceiptAccount(
                        FamilyIncomeReceiptAccountTypes.HouseholdAccount,
                        FamilyIncomeReceiptAccountTypes.GetNamePl(
                            FamilyIncomeReceiptAccountTypes.HouseholdAccount),
                        account.Id,
                        account.Name,
                        account.CurrencyCode,
                        FamilyFinanceMoney.FromMinorUnits(
                            balances.GetValueOrDefault(account.Id)))));
        }

        return new FamilyChildIncomeReceiptForm(
            familyGroupId,
            familyGroup.Name,
            rule.Id,
            rule.Name,
            FamilyIncomeKinds.GetNamePl(rule.IncomeKindCode),
            rule.BeneficiaryPersonId,
            child.DisplayName,
            year,
            month,
            periodKey,
            FamilyFinanceMoney.FromMinorUnits(rule.PlannedAmountMinor),
            BuildPlannedDate(year, month, rule.DueDay),
            accounts);
    }

    public async Task<FamilyChildIncomeReceiptResult> ConfirmChildIncomeReceiptAsync(
        ConfirmFamilyChildIncomeReceiptRequest request,
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

        ValidatePeriod(request.Year, request.Month);

        if (!FamilyIncomeReceiptAccountTypes.IsValid(request.AccountType))
        {
            throw new ArgumentException(
                "Wybierz poprawny rodzaj konta dla wpływu.");
        }

        var actor = await GetActorContextAsync(
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

        var rule = await GetChildIncomeRuleByIdAsync(
            request.RuleId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "Nie znaleziono przychodu dziecka.");

        if (rule.FamilyGroupId != request.FamilyGroupId)
        {
            throw new UnauthorizedAccessException(
                "Ten przychód należy do innej rodziny.");
        }

        var monthStart = new DateTime(
            request.Year,
            request.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        if (!AppliesInMonth(rule, monthStart))
        {
            throw new InvalidOperationException(
                "Ten przychód nie przypada w wybranym miesiącu.");
        }

        var periodKey = $"{request.Year:D4}-{request.Month:D2}";

        if (await GetChildIncomeReceiptByRulePeriodAsync(
                rule.Id,
                periodKey,
                cancellationToken) is not null)
        {
            throw new InvalidOperationException(
                "Wpływ tego przychodu w wybranym miesiącu został już potwierdzony.");
        }

        var receivedAtUtc = request.ReceivedAtUtc == default
            ? DateTime.UtcNow
            : NormalizeUtcDate(request.ReceivedAtUtc);

        if (receivedAtUtc.Date > DateTime.UtcNow.Date)
        {
            throw new ArgumentException(
                "Data wpływu nie może być późniejsza niż dzisiaj.");
        }

        if (receivedAtUtc.Date < monthStart.Date)
        {
            throw new ArgumentException(
                "Data wpływu nie może być wcześniejsza niż miesiąc, którego dotyczy potwierdzany przychód.");
        }

        var now = DateTime.UtcNow;
        var receiptId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        string sourceType;
        Guid? receivedIntoPersonId;

        await using var dbTransaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        if (request.AccountType == FamilyIncomeReceiptAccountTypes.PersonalAccount)
        {
            await PermissionEnforcement.EnsureUserHasAsync(
                dbContext,
                actorUserId,
                SystemPermissions.FinancePersonalManageOwn,
                cancellationToken);

            var account = await dbContext.PersonalFinancialAccounts
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == request.AccountId &&
                        x.OwnerPersonId == actor.PersonId &&
                        x.IsActive &&
                        x.CurrencyCode == "PLN",
                    cancellationToken)
                ?? throw new UnauthorizedAccessException(
                    "Wybrane konto osobiste nie istnieje, jest nieaktywne albo nie należy do zalogowanego użytkownika.");

            dbContext.PersonalFinancialTransactions.Add(
                new PersonalFinancialTransaction
                {
                    Id = sourceId,
                    AccountId = account.Id,
                    OwnerPersonId = actor.PersonId,
                    KindCode = PersonalTransactionKinds.Income,
                    AmountMinor = rule.PlannedAmountMinor,
                    OccurredAtUtc = receivedAtUtc,
                    CategoryCode = PersonalFinanceCategories.OtherIncome,
                    Counterparty = "Świadczenie rodzinne",
                    Description = $"{rule.Name} — {periodKey}",
                    CreatedByUserId = actorUserId,
                    CreatedAtUtc = now
                });

            account.UpdatedAtUtc = now;
            sourceType = FamilyBudgetSourceTypes.PersonalTransaction;
            receivedIntoPersonId = actor.PersonId;
        }
        else if (request.AccountType == FamilyIncomeReceiptAccountTypes.FamilySharedAccount)
        {
            await PermissionEnforcement.EnsureUserHasAsync(
                dbContext,
                actorUserId,
                SystemPermissions.FinancePersonalManageOwn,
                cancellationToken);

            var sharedRow = (await GetSharedAccountRowsForPersonAsync(
                    actor.PersonId,
                    cancellationToken))
                .SingleOrDefault(x =>
                    x.FamilyGroupId == request.FamilyGroupId &&
                    x.PersonalAccountId == request.AccountId)
                ?? throw new UnauthorizedAccessException(
                    "Wybrane wspólne konto nie należy do tej rodziny albo nie jesteś jego właścicielem lub współwłaścicielem.");

            var account = await dbContext.PersonalFinancialAccounts
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == sharedRow.PersonalAccountId &&
                        x.IsActive &&
                        x.CurrencyCode == "PLN",
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "Wybrane wspólne konto rodziny jest nieaktywne.");

            dbContext.PersonalFinancialTransactions.Add(
                new PersonalFinancialTransaction
                {
                    Id = sourceId,
                    AccountId = account.Id,
                    OwnerPersonId = sharedRow.OwnerPersonId,
                    KindCode = PersonalTransactionKinds.Income,
                    AmountMinor = rule.PlannedAmountMinor,
                    OccurredAtUtc = receivedAtUtc,
                    CategoryCode = PersonalFinanceCategories.OtherIncome,
                    Counterparty = "Świadczenie rodzinne",
                    Description = $"{rule.Name} — {periodKey}",
                    CreatedByUserId = actorUserId,
                    CreatedAtUtc = now
                });

            account.UpdatedAtUtc = now;
            sourceType = FamilyBudgetSourceTypes.PersonalTransaction;
            receivedIntoPersonId = null;
        }
        else
        {
            await PermissionEnforcement.EnsureUserHasAsync(
                dbContext,
                actorUserId,
                SystemPermissions.FinanceHouseholdManage,
                cancellationToken);

            var account = await dbContext.HouseholdAccounts
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == request.AccountId &&
                        x.HouseholdId == actor.HouseholdId.Value &&
                        x.IsActive &&
                        x.CurrencyCode == "PLN",
                    cancellationToken)
                ?? throw new UnauthorizedAccessException(
                    "Wybrane konto domowe nie istnieje albo jest nieaktywne.");

            dbContext.HouseholdEntries.Add(
                new HouseholdEntry
                {
                    Id = sourceId,
                    HouseholdId = actor.HouseholdId.Value,
                    AccountId = account.Id,
                    EntryTypeCode = HouseholdEntryTypes.Income,
                    AmountMinor = rule.PlannedAmountMinor,
                    OccurredAtUtc = receivedAtUtc,
                    CategoryCode = HouseholdFinanceCategories.HouseholdIncome,
                    Description = $"{rule.Name} — {periodKey}",
                    SourceType = "FamilyChildIncomeReceipt",
                    SourceId = receiptId.ToString(),
                    CreatedByUserId = actorUserId,
                    CreatedAtUtc = now
                });

            account.UpdatedAtUtc = now;
            sourceType = FamilyBudgetSourceTypes.HouseholdEntry;
            receivedIntoPersonId = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var inserted = await ExecuteAsync(
            """
            INSERT INTO FamilyIncomeReceipts
                (Id, FamilyGroupId, RuleId, PeriodKey, BeneficiaryPersonId,
                 SourceType, SourceId, ReceivedIntoAccountId, ReceivedIntoPersonId,
                 AmountMinor, ReceivedByUserId, ReceivedAtUtc, CreatedAtUtc)
            VALUES
                ($id, $groupId, $ruleId, $periodKey, $beneficiaryPersonId,
                 $sourceType, $sourceId, $accountId, $personId,
                 $amountMinor, $actorUserId, $receivedAtUtc, $now);
            """,
            [
                P("$id", receiptId),
                P("$groupId", request.FamilyGroupId),
                P("$ruleId", rule.Id),
                P("$periodKey", periodKey),
                P("$beneficiaryPersonId", rule.BeneficiaryPersonId),
                P("$sourceType", sourceType),
                P("$sourceId", sourceId),
                P("$accountId", request.AccountId),
                P("$personId", receivedIntoPersonId),
                P("$amountMinor", rule.PlannedAmountMinor),
                P("$actorUserId", actorUserId),
                P("$receivedAtUtc", receivedAtUtc),
                P("$now", now)
            ],
            cancellationToken);

        if (inserted != 1)
        {
            throw new InvalidOperationException(
                "Nie udało się zapisać potwierdzenia wpływu.");
        }

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.8.5.ChildIncomeReceiptConfirmed",
                EntityType: "FamilyIncomeReceipt",
                EntityId: receiptId.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Potwierdzono rzeczywisty wpływ przychodu przypisanego do dziecka i zaksięgowano go na wybranym koncie. Kwota i nazwa konta nie są zapisywane w audycie."),
            cancellationToken);

        await dbTransaction.CommitAsync(cancellationToken);

        return new FamilyChildIncomeReceiptResult(
            receiptId,
            sourceType,
            sourceId);
    }



    public async Task<FamilyChildContributionPaymentForm?> GetChildContributionPaymentFormAsync(
        Guid familyGroupId,
        Guid obligationId,
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
            throw new InvalidOperationException(
                "Brak aktywnego gospodarstwa.");
        }

        var familyGroup =
            await EnsureActiveMembershipAsync(
                familyGroupId,
                actor.HouseholdId.Value,
                actor.PersonId,
                cancellationToken);

        var familyChildren =
            (await GetActiveMembersAsync(
                familyGroupId,
                cancellationToken))
            .Where(x =>
                x.FamilyRoleCode == FamilyRoles.Child)
            .ToDictionary(
                x => x.PersonId,
                x => x.DisplayName);

        if (familyChildren.Count == 0)
        {
            return null;
        }

        var row =
            await (
                from obligation in dbContext.HouseholdContributionObligations
                    .AsNoTracking()
                join rule in dbContext.HouseholdContributionRules
                    .AsNoTracking()
                    on obligation.ContributionRuleId equals rule.Id
                join householdMember in dbContext.HouseholdMembers
                    .AsNoTracking()
                    on obligation.HouseholdMemberId equals householdMember.Id
                join childPerson in dbContext.People
                    .AsNoTracking()
                    on householdMember.PersonId equals childPerson.Id
                join targetAccount in dbContext.HouseholdAccounts
                    .AsNoTracking()
                    on obligation.TargetHouseholdAccountId equals targetAccount.Id
                where
                    obligation.Id == obligationId &&
                    obligation.HouseholdId == actor.HouseholdId.Value &&
                    rule.HouseholdId == actor.HouseholdId.Value &&
                    rule.ModeCode == HouseholdContributionModes.FixedAmount &&
                    !rule.IncomeRuleId.HasValue &&
                    householdMember.HouseholdId == actor.HouseholdId.Value &&
                    householdMember.IsActive &&
                    childPerson.IsActive &&
                    childPerson.PersonTypeCode == PersonTypes.Child &&
                    targetAccount.IsActive
                select new
                {
                    Obligation = obligation,
                    Rule = rule,
                    HouseholdMember = householdMember,
                    Person = childPerson,
                    TargetAccount = targetAccount
                })
                .SingleOrDefaultAsync(
                    cancellationToken);

        if (row is null ||
            !familyChildren.TryGetValue(
                row.Person.Id,
                out var childDisplayName))
        {
            return null;
        }

        if (row.Obligation.StatusCode ==
                HouseholdContributionStatuses.Cancelled ||
            row.Obligation.StatusCode ==
                HouseholdContributionStatuses.Corrected)
        {
            throw new InvalidOperationException(
                "Tego zobowiązania dziecka nie można już opłacić.");
        }

        var outstandingMinor =
            Math.Max(
                0,
                row.Obligation.AmountMinor -
                row.Obligation.PaidAmountMinor);

        if (outstandingMinor <= 0)
        {
            throw new InvalidOperationException(
                "Składka dziecka za ten miesiąc jest już opłacona.");
        }

        var sources =
            new List<FamilyChildContributionPaymentSource>();

        var canUseSharedAccounts =
            await PermissionEnforcement.HasUserAsync(
                dbContext,
                actorUserId,
                SystemPermissions.FinancePersonalManageOwn,
                cancellationToken);

        var canUseHouseholdAccounts =
            await PermissionEnforcement.HasUserAsync(
                dbContext,
                actorUserId,
                SystemPermissions.FinanceHouseholdManage,
                cancellationToken);

        IReadOnlyList<SharedAccountRow> sharedRows =
            canUseSharedAccounts
                ? await GetSharedAccountRowsForGroupAsync(
                    familyGroupId,
                    cancellationToken)
                : [];

        var availableSharedRows =
            sharedRows
                .Where(x =>
                    x.ClosedAtUtc is null &&
                    (x.OwnerPersonId == actor.PersonId ||
                     x.CoOwnerPersonId == actor.PersonId))
                .ToArray();

        if (availableSharedRows.Length > 0)
        {
            var personalAccountIds =
                availableSharedRows
                    .Select(x => x.PersonalAccountId)
                    .Distinct()
                    .ToArray();

            var sharedAccounts =
                await dbContext.PersonalFinancialAccounts
                    .AsNoTracking()
                    .Where(x =>
                        personalAccountIds.Contains(x.Id) &&
                        x.IsActive &&
                        x.CurrencyCode == row.TargetAccount.CurrencyCode)
                    .ToDictionaryAsync(
                        x => x.Id,
                        cancellationToken);

            var sharedBalances =
                await dbContext.PersonalFinancialTransactions
                    .AsNoTracking()
                    .Where(x =>
                        personalAccountIds.Contains(x.AccountId))
                    .GroupBy(x => x.AccountId)
                    .Select(x => new
                    {
                        AccountId = x.Key,
                        BalanceMinor = x.Sum(y => y.AmountMinor)
                    })
                    .ToDictionaryAsync(
                        x => x.AccountId,
                        x => x.BalanceMinor,
                        cancellationToken);

            foreach (var sharedRow in availableSharedRows)
            {
                if (!sharedAccounts.TryGetValue(
                        sharedRow.PersonalAccountId,
                        out var sharedAccount))
                {
                    continue;
                }

                sources.Add(
                    new FamilyChildContributionPaymentSource(
                        FamilyChildContributionPaymentSourceTypes.FamilySharedAccount,
                        FamilyChildContributionPaymentSourceTypes.GetNamePl(
                            FamilyChildContributionPaymentSourceTypes.FamilySharedAccount),
                        sharedRow.Id,
                        sharedAccount.Name,
                        sharedAccount.CurrencyCode,
                        PersonalFinanceMoney.FromMinorUnits(
                            sharedBalances.GetValueOrDefault(
                                sharedAccount.Id)),
                        false));
            }
        }

        HouseholdAccount[] householdAccounts =
            canUseHouseholdAccounts
                ? await dbContext.HouseholdAccounts
                    .AsNoTracking()
                    .Where(x =>
                        x.HouseholdId == actor.HouseholdId.Value &&
                        x.IsActive &&
                        x.CurrencyCode == row.TargetAccount.CurrencyCode)
                    .OrderBy(x => x.Name)
                    .ToArrayAsync(
                        cancellationToken)
                : [];

        var householdAccountIds =
            householdAccounts
                .Select(x => x.Id)
                .ToArray();

        var householdBalances =
            householdAccountIds.Length == 0
                ? new Dictionary<Guid, long>()
                : await dbContext.HouseholdEntries
                    .AsNoTracking()
                    .Where(x =>
                        householdAccountIds.Contains(x.AccountId) &&
                        x.HouseholdId == actor.HouseholdId.Value)
                    .GroupBy(x => x.AccountId)
                    .Select(x => new
                    {
                        AccountId = x.Key,
                        BalanceMinor = x.Sum(y => y.AmountMinor)
                    })
                    .ToDictionaryAsync(
                        x => x.AccountId,
                        x => x.BalanceMinor,
                        cancellationToken);

        foreach (var householdAccount in householdAccounts)
        {
            sources.Add(
                new FamilyChildContributionPaymentSource(
                    FamilyChildContributionPaymentSourceTypes.HouseholdAccount,
                    FamilyChildContributionPaymentSourceTypes.GetNamePl(
                        FamilyChildContributionPaymentSourceTypes.HouseholdAccount),
                    householdAccount.Id,
                    householdAccount.Name,
                    householdAccount.CurrencyCode,
                    HouseholdFinanceMoney.FromMinorUnits(
                        householdBalances.GetValueOrDefault(
                            householdAccount.Id)),
                    householdAccount.Id ==
                        row.TargetAccount.Id));
        }

        return new FamilyChildContributionPaymentForm(
            familyGroup.Id,
            familyGroup.Name,
            row.Obligation.Id,
            row.Person.Id,
            childDisplayName,
            row.Obligation.PeriodKey,
            HouseholdFinanceMoney.FromMinorUnits(
                row.Obligation.AmountMinor),
            HouseholdFinanceMoney.FromMinorUnits(
                row.Obligation.PaidAmountMinor),
            HouseholdFinanceMoney.FromMinorUnits(
                outstandingMinor),
            row.Obligation.DueDateUtc,
            row.TargetAccount.Id,
            row.TargetAccount.Name,
            row.TargetAccount.CurrencyCode,
            sources);
    }

    public async Task<FamilyChildContributionPaymentResult> PayChildContributionAsync(
        PayFamilyChildContributionRequest request,
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

        if (!FamilyChildContributionPaymentSourceTypes.IsValid(
                request.SourceType))
        {
            throw new ArgumentException(
                "Wybrano nieprawidłowe źródło składki dziecka.");
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

        var familyChildren =
            (await GetActiveMembersAsync(
                request.FamilyGroupId,
                cancellationToken))
            .Where(x =>
                x.FamilyRoleCode == FamilyRoles.Child)
            .ToDictionary(
                x => x.PersonId,
                x => x.DisplayName);

        var paidAtUtc =
            request.PaidAtUtc == default
                ? DateTime.UtcNow
                : NormalizeUtcDate(
                    request.PaidAtUtc);

        if (paidAtUtc.Date > DateTime.UtcNow.Date)
        {
            throw new ArgumentException(
                "Data przekazania składki nie może być późniejsza niż dzisiaj.");
        }

        if (request.SourceType ==
            FamilyChildContributionPaymentSourceTypes.FamilySharedAccount)
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
                SystemPermissions.FinanceHouseholdManage,
                cancellationToken);
        }

        await using var dbTransaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var row =
            await (
                from obligation in dbContext.HouseholdContributionObligations
                join rule in dbContext.HouseholdContributionRules
                    on obligation.ContributionRuleId equals rule.Id
                join householdMember in dbContext.HouseholdMembers
                    on obligation.HouseholdMemberId equals householdMember.Id
                join childPerson in dbContext.People
                    on householdMember.PersonId equals childPerson.Id
                join targetAccount in dbContext.HouseholdAccounts
                    on obligation.TargetHouseholdAccountId equals targetAccount.Id
                where
                    obligation.Id == request.ObligationId &&
                    obligation.HouseholdId == actor.HouseholdId.Value &&
                    rule.HouseholdId == actor.HouseholdId.Value &&
                    rule.ModeCode == HouseholdContributionModes.FixedAmount &&
                    !rule.IncomeRuleId.HasValue &&
                    householdMember.HouseholdId == actor.HouseholdId.Value &&
                    householdMember.IsActive &&
                    childPerson.IsActive &&
                    childPerson.PersonTypeCode == PersonTypes.Child &&
                    targetAccount.IsActive
                select new
                {
                    Obligation = obligation,
                    Rule = rule,
                    HouseholdMember = householdMember,
                    Person = childPerson,
                    TargetAccount = targetAccount
                })
                .SingleOrDefaultAsync(
                    cancellationToken)
            ?? throw new InvalidOperationException(
                "Nie znaleziono składki dziecka do przekazania.");

        if (!familyChildren.TryGetValue(
                row.Person.Id,
                out var childDisplayName))
        {
            throw new UnauthorizedAccessException(
                "Dziecko przypisane do składki nie jest aktywnym członkiem tej rodziny.");
        }

        var obligationMonth =
            new DateTime(
                row.Obligation.DueDateUtc.Year,
                row.Obligation.DueDateUtc.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var currentMonth =
            new DateTime(
                DateTime.UtcNow.Year,
                DateTime.UtcNow.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        if (obligationMonth > currentMonth)
        {
            throw new InvalidOperationException(
                "Nie można przekazać składki dziecka za przyszły miesiąc.");
        }

        if (row.Obligation.StatusCode ==
                HouseholdContributionStatuses.Cancelled ||
            row.Obligation.StatusCode ==
                HouseholdContributionStatuses.Corrected)
        {
            throw new InvalidOperationException(
                "Tego zobowiązania dziecka nie można już opłacić.");
        }

        var outstandingMinor =
            Math.Max(
                0,
                row.Obligation.AmountMinor -
                row.Obligation.PaidAmountMinor);

        if (outstandingMinor <= 0)
        {
            throw new InvalidOperationException(
                "Składka dziecka za ten miesiąc jest już opłacona.");
        }

        var now =
            DateTime.UtcNow;

        var paymentId =
            Guid.NewGuid();

        Guid? sourceTransactionId = null;
        Guid? householdEntryId = null;

        if (request.SourceType ==
            FamilyChildContributionPaymentSourceTypes.FamilySharedAccount)
        {
            var sharedRow =
                (await GetSharedAccountRowsForGroupAsync(
                    request.FamilyGroupId,
                    cancellationToken))
                .SingleOrDefault(x =>
                    x.Id == request.SourceId &&
                    x.ClosedAtUtc is null &&
                    (x.OwnerPersonId == actor.PersonId ||
                     x.CoOwnerPersonId == actor.PersonId))
                ?? throw new UnauthorizedAccessException(
                    "Nie jesteś właścicielem ani współwłaścicielem wybranego wspólnego konta rodziny.");

            var sourceAccount =
                await dbContext.PersonalFinancialAccounts
                    .SingleOrDefaultAsync(
                        x =>
                            x.Id == sharedRow.PersonalAccountId &&
                            x.IsActive,
                        cancellationToken)
                ?? throw new InvalidOperationException(
                    "Wybrane wspólne konto rodziny jest nieaktywne.");

            if (!string.Equals(
                    sourceAccount.CurrencyCode,
                    row.TargetAccount.CurrencyCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Waluta wspólnego konta rodziny i konta domu nie jest zgodna.");
            }

            var sourceBalanceMinor =
                await dbContext.PersonalFinancialTransactions
                    .Where(x =>
                        x.AccountId == sourceAccount.Id)
                    .SumAsync(
                        x => x.AmountMinor,
                        cancellationToken);

            if (sourceBalanceMinor < outstandingMinor)
            {
                throw new InvalidOperationException(
                    "Na wybranym wspólnym koncie rodziny nie ma wystarczających środków.");
            }

            sourceTransactionId =
                Guid.NewGuid();

            householdEntryId =
                Guid.NewGuid();

            dbContext.PersonalFinancialTransactions.Add(
                new PersonalFinancialTransaction
                {
                    Id = sourceTransactionId.Value,
                    AccountId = sourceAccount.Id,
                    OwnerPersonId = sharedRow.OwnerPersonId,
                    KindCode = PersonalTransactionKinds.Expense,
                    AmountMinor = -outstandingMinor,
                    OccurredAtUtc = paidAtUtc,
                    CategoryCode = PersonalFinanceCategories.HouseholdContribution,
                    Counterparty = "Budżet domu",
                    Description =
                        $"Składka za {childDisplayName} za {row.Obligation.PeriodKey}",
                    CreatedByUserId = actorUserId,
                    CreatedAtUtc = now
                });

            dbContext.HouseholdEntries.Add(
                new HouseholdEntry
                {
                    Id = householdEntryId.Value,
                    HouseholdId = actor.HouseholdId.Value,
                    AccountId = row.TargetAccount.Id,
                    EntryTypeCode = HouseholdEntryTypes.MemberContribution,
                    AmountMinor = outstandingMinor,
                    OccurredAtUtc = paidAtUtc,
                    CategoryCode = HouseholdFinanceCategories.HouseholdIncome,
                    Description =
                        $"Składka za dziecko {childDisplayName} za {row.Obligation.PeriodKey}",
                    SourceType = "FamilyChildContributionPayment",
                    SourceId = paymentId.ToString(),
                    CreatedByUserId = actorUserId,
                    CreatedAtUtc = now
                });

            sourceAccount.UpdatedAtUtc = now;
            row.TargetAccount.UpdatedAtUtc = now;
        }
        else
        {
            var sourceAccount =
                await dbContext.HouseholdAccounts
                    .SingleOrDefaultAsync(
                        x =>
                            x.Id == request.SourceId &&
                            x.HouseholdId == actor.HouseholdId.Value &&
                            x.IsActive,
                        cancellationToken)
                ?? throw new InvalidOperationException(
                    "Wybrane konto domu nie istnieje albo jest nieaktywne.");

            if (!string.Equals(
                    sourceAccount.CurrencyCode,
                    row.TargetAccount.CurrencyCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Waluta konta źródłowego i docelowego domu nie jest zgodna.");
            }

            var sourceBalanceMinor =
                await dbContext.HouseholdEntries
                    .Where(x =>
                        x.HouseholdId == actor.HouseholdId.Value &&
                        x.AccountId == sourceAccount.Id)
                    .SumAsync(
                        x => x.AmountMinor,
                        cancellationToken);

            if (sourceBalanceMinor < outstandingMinor)
            {
                throw new InvalidOperationException(
                    "Na wybranym koncie domu nie ma wystarczających środków.");
            }

            if (sourceAccount.Id != row.TargetAccount.Id)
            {
                var sourceEntryId =
                    Guid.NewGuid();

                var targetEntryId =
                    Guid.NewGuid();

                sourceTransactionId =
                    sourceEntryId;

                householdEntryId =
                    targetEntryId;

                dbContext.HouseholdEntries.AddRange(
                    new HouseholdEntry
                    {
                        Id = sourceEntryId,
                        HouseholdId = actor.HouseholdId.Value,
                        AccountId = sourceAccount.Id,
                        EntryTypeCode = HouseholdEntryTypes.TransferOut,
                        AmountMinor = -outstandingMinor,
                        OccurredAtUtc = paidAtUtc,
                        CategoryCode = null,
                        Description =
                            $"Pokrycie składki za {childDisplayName} za {row.Obligation.PeriodKey}",
                        SourceType = "FamilyChildContributionCoverage",
                        SourceId = paymentId.ToString(),
                        CreatedByUserId = actorUserId,
                        CreatedAtUtc = now
                    },
                    new HouseholdEntry
                    {
                        Id = targetEntryId,
                        HouseholdId = actor.HouseholdId.Value,
                        AccountId = row.TargetAccount.Id,
                        EntryTypeCode = HouseholdEntryTypes.TransferIn,
                        AmountMinor = outstandingMinor,
                        OccurredAtUtc = paidAtUtc,
                        CategoryCode = null,
                        Description =
                            $"Składka za {childDisplayName} za {row.Obligation.PeriodKey}",
                        SourceType = "FamilyChildContributionCoverage",
                        SourceId = paymentId.ToString(),
                        CreatedByUserId = actorUserId,
                        CreatedAtUtc = now
                    });

                sourceAccount.UpdatedAtUtc = now;
                row.TargetAccount.UpdatedAtUtc = now;
            }
            else
            {
                row.TargetAccount.UpdatedAtUtc = now;
            }
        }

        row.Obligation.PaidAmountMinor +=
            outstandingMinor;

        row.Obligation.StatusCode =
            HouseholdContributionStatuses.Paid;

        row.Obligation.UpdatedAtUtc =
            now;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.8.7.ChildHouseholdContributionPaid",
                EntityType: "HouseholdContributionObligation",
                EntityId: row.Obligation.Id.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Przekazano za dziecko składkę dla domu. Kwota i nazwa konta źródłowego nie są zapisywane w audycie."),
            cancellationToken);

        await dbTransaction.CommitAsync(
            cancellationToken);

        return new FamilyChildContributionPaymentResult(
            row.Obligation.Id,
            request.SourceType,
            request.SourceId,
            sourceTransactionId,
            householdEntryId,
            HouseholdFinanceMoney.FromMinorUnits(
                outstandingMinor));
    }


    public async Task<CreateFamilySharedAccountForm?> GetCreateSharedAccountFormAsync(
        Guid familyGroupId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            FamilyFinancePermissions.Manage,
            cancellationToken);

        var actor = await GetActorContextAsync(
            actorUserId,
            cancellationToken);

        if (actor.HouseholdId is null)
        {
            throw new InvalidOperationException(
                "Brak aktywnego gospodarstwa.");
        }

        var familyGroup = await EnsureActiveMembershipAsync(
            familyGroupId,
            actor.HouseholdId.Value,
            actor.PersonId,
            cancellationToken);

        var members = await GetActiveMembersAsync(
            familyGroupId,
            cancellationToken);

        var adultPersonIds = members
            .Where(x => x.FamilyRoleCode == FamilyRoles.Adult)
            .Select(x => x.PersonId)
            .Distinct()
            .ToArray();

        var userPersonIds = adultPersonIds.Length == 0
            ? Array.Empty<Guid>()
            : await dbContext.UserAccounts
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    adultPersonIds.Contains(x.PersonId))
                .Select(x => x.PersonId)
                .Distinct()
                .ToArrayAsync(cancellationToken);

        var eligible = userPersonIds.ToHashSet();

        var adults = members
            .Where(x =>
                x.FamilyRoleCode == FamilyRoles.Adult &&
                eligible.Contains(x.PersonId))
            .OrderBy(x => x.DisplayName)
            .Select(x =>
                new FamilySharedAccountPersonOption(
                    x.PersonId,
                    x.DisplayName))
            .ToArray();

        return new CreateFamilySharedAccountForm(
            familyGroup.Id,
            familyGroup.Name,
            adults);
    }

    public async Task<Guid> CreateSharedAccountAsync(
        CreateFamilySharedAccountRequest request,
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

        var actor = await GetActorContextAsync(
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

        var name = NormalizeRequiredText(
            request.Name,
            "Nazwa wspólnego konta",
            120);

        if (!PersonalAccountTypes.IsValid(request.AccountTypeCode))
        {
            throw new ArgumentException(
                "Wybrano nieprawidłowy typ konta.");
        }

        if (request.InitialBalance < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.InitialBalance),
                "Saldo początkowe nie może być ujemne.");
        }

        var initialBalanceMinor =
            PersonalFinanceMoney.ToMinorUnits(
                request.InitialBalance);

        if (request.OwnerPersonId == request.CoOwnerPersonId)
        {
            throw new ArgumentException(
                "Właściciel i współwłaściciel muszą być dwiema różnymi osobami.");
        }

        var members = await GetActiveMembersAsync(
            request.FamilyGroupId,
            cancellationToken);

        var adultIds = members
            .Where(x => x.FamilyRoleCode == FamilyRoles.Adult)
            .Select(x => x.PersonId)
            .ToHashSet();

        if (!adultIds.Contains(request.OwnerPersonId) ||
            !adultIds.Contains(request.CoOwnerPersonId))
        {
            throw new ArgumentException(
                "Właściciel i współwłaściciel muszą być aktywnymi dorosłymi członkami tej rodziny.");
        }

        var activeUserPersonIds = await dbContext.UserAccounts
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                (x.PersonId == request.OwnerPersonId ||
                 x.PersonId == request.CoOwnerPersonId))
            .Select(x => x.PersonId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        if (activeUserPersonIds.Length != 2)
        {
            throw new InvalidOperationException(
                "Właściciel i współwłaściciel muszą mieć aktywne konta użytkownika Domio.");
        }

        var existingSharedAccounts =
            await BuildSharedAccountSummariesForGroupAsync(
                request.FamilyGroupId,
                actor.PersonId,
                cancellationToken);

        if (existingSharedAccounts.Any(x =>
                x.IsActive &&
                string.Equals(
                    x.AccountName,
                    name,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "W tej rodzinie istnieje już aktywne wspólne konto o takiej nazwie.");
        }

        var now = DateTime.UtcNow;
        var sharedAccountId = Guid.NewGuid();
        var account = new PersonalFinancialAccount
        {
            Id = Guid.NewGuid(),
            OwnerPersonId = request.OwnerPersonId,
            Name = name,
            AccountTypeCode = request.AccountTypeCode,
            CurrencyCode = "PLN",
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await using var dbTransaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        dbContext.PersonalFinancialAccounts.Add(account);

        if (initialBalanceMinor > 0)
        {
            dbContext.PersonalFinancialTransactions.Add(
                new PersonalFinancialTransaction
                {
                    Id = Guid.NewGuid(),
                    AccountId = account.Id,
                    OwnerPersonId = request.OwnerPersonId,
                    KindCode = PersonalTransactionKinds.OpeningBalance,
                    AmountMinor = initialBalanceMinor,
                    OccurredAtUtc = now,
                    Description = "Saldo początkowe wspólnego konta rodziny",
                    CreatedByUserId = actorUserId,
                    CreatedAtUtc = now
                });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var inserted = await ExecuteAsync(
            """
            INSERT INTO FamilySharedAccounts
                (Id, FamilyGroupId, PersonalAccountId, OwnerPersonId,
                 CoOwnerPersonId, CreatedByUserId, CreatedAtUtc, ClosedAtUtc)
            VALUES
                ($id, $groupId, $accountId, $ownerPersonId,
                 $coOwnerPersonId, $actorUserId, $createdAtUtc, NULL);
            """,
            [
                P("$id", sharedAccountId),
                P("$groupId", request.FamilyGroupId),
                P("$accountId", account.Id),
                P("$ownerPersonId", request.OwnerPersonId),
                P("$coOwnerPersonId", request.CoOwnerPersonId),
                P("$actorUserId", actorUserId),
                P("$createdAtUtc", now)
            ],
            cancellationToken);

        if (inserted != 1)
        {
            throw new InvalidOperationException(
                "Nie udało się utworzyć wspólnego konta rodziny.");
        }

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.8.6.FamilySharedAccountCreated",
                EntityType: "FamilySharedAccount",
                EntityId: sharedAccountId.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Utworzono wspólne konto rodziny z właścicielem i współwłaścicielem. Kwota początkowa i nazwa konta nie są zapisywane w audycie."),
            cancellationToken);

        await dbTransaction.CommitAsync(cancellationToken);

        return sharedAccountId;
    }

    public async Task<IReadOnlyList<FamilySharedAccountSummary>> GetSharedAccountsForUserAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinancePersonalViewOwn,
            cancellationToken);

        var actor = await GetActorContextAsync(
            actorUserId,
            cancellationToken);

        var rows = await GetSharedAccountRowsForPersonAsync(
            actor.PersonId,
            cancellationToken);

        return await BuildSharedAccountSummariesAsync(
            rows,
            actor.PersonId,
            cancellationToken);
    }

    public async Task<FamilySharedAccountOperationForm?> GetSharedAccountOperationFormAsync(
        Guid sharedAccountId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinancePersonalViewOwn,
            cancellationToken);

        var actor = await GetActorContextAsync(
            actorUserId,
            cancellationToken);

        var row = await GetSharedAccountRowForPersonAsync(
            sharedAccountId,
            actor.PersonId,
            cancellationToken);

        if (row is null)
        {
            return null;
        }

        var account = await dbContext.PersonalFinancialAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.Id == row.PersonalAccountId &&
                    x.IsActive,
                cancellationToken);

        if (account is null)
        {
            return null;
        }

        var balanceMinor = await dbContext.PersonalFinancialTransactions
            .AsNoTracking()
            .Where(x => x.AccountId == account.Id)
            .SumAsync(x => x.AmountMinor, cancellationToken);

        var roleCode = row.OwnerPersonId == actor.PersonId
            ? FamilySharedAccountRoles.Owner
            : FamilySharedAccountRoles.CoOwner;

        return new FamilySharedAccountOperationForm(
            row.Id,
            row.FamilyGroupId,
            row.FamilyGroupName,
            account.Id,
            account.Name,
            account.CurrencyCode,
            PersonalFinanceMoney.FromMinorUnits(balanceMinor),
            FamilySharedAccountRoles.GetNamePl(roleCode));
    }

    public async Task<Guid> PostSharedAccountOperationAsync(
        PostFamilySharedAccountOperationRequest request,
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

        var actor = await GetActorContextAsync(
            actorUserId,
            cancellationToken);

        var row = await GetSharedAccountRowForPersonAsync(
            request.SharedAccountId,
            actor.PersonId,
            cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Nie jesteś właścicielem ani współwłaścicielem tego wspólnego konta.");

        var account = await dbContext.PersonalFinancialAccounts
            .SingleOrDefaultAsync(
                x =>
                    x.Id == row.PersonalAccountId &&
                    x.IsActive,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Wspólne konto jest nieaktywne.");

        if (request.KindCode != PersonalTransactionKinds.Income &&
            request.KindCode != PersonalTransactionKinds.Expense)
        {
            throw new ArgumentException(
                "Na wspólnym koncie można ręcznie dodać przychód albo wydatek.");
        }

        if (request.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Amount),
                "Kwota musi być większa od zera.");
        }

        var amountMinor =
            PersonalFinanceMoney.ToMinorUnits(request.Amount);

        var categoryCode = string.IsNullOrWhiteSpace(request.CategoryCode)
            ? request.KindCode == PersonalTransactionKinds.Income
                ? PersonalFinanceCategories.OtherIncome
                : PersonalFinanceCategories.OtherExpense
            : request.CategoryCode.Trim();

        if (!PersonalFinanceCategories.IsValid(categoryCode))
        {
            throw new ArgumentException(
                "Wybrano nieprawidłową kategorię.");
        }

        var description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : NormalizeRequiredText(
                request.Description,
                "Opis",
                500);

        var counterparty = string.IsNullOrWhiteSpace(request.Counterparty)
            ? null
            : NormalizeRequiredText(
                request.Counterparty,
                "Kontrahent",
                200);

        var occurredAtUtc = request.OccurredAtUtc == default
            ? DateTime.UtcNow
            : NormalizeUtcDate(request.OccurredAtUtc);

        if (occurredAtUtc.Date > DateTime.UtcNow.Date)
        {
            throw new ArgumentException(
                "Data operacji nie może być późniejsza niż dzisiaj.");
        }

        var currentBalanceMinor = await dbContext.PersonalFinancialTransactions
            .AsNoTracking()
            .Where(x => x.AccountId == account.Id)
            .SumAsync(x => x.AmountMinor, cancellationToken);

        if (request.KindCode == PersonalTransactionKinds.Expense &&
            currentBalanceMinor < amountMinor)
        {
            throw new InvalidOperationException(
                "Niewystarczające środki na wspólnym koncie.");
        }

        var signedAmountMinor =
            request.KindCode == PersonalTransactionKinds.Expense
                ? -amountMinor
                : amountMinor;

        var now = DateTime.UtcNow;
        var transactionId = Guid.NewGuid();

        dbContext.PersonalFinancialTransactions.Add(
            new PersonalFinancialTransaction
            {
                Id = transactionId,
                AccountId = account.Id,
                OwnerPersonId = row.OwnerPersonId,
                KindCode = request.KindCode,
                AmountMinor = signedAmountMinor,
                OccurredAtUtc = occurredAtUtc,
                CategoryCode = categoryCode,
                Counterparty = counterparty,
                Description = description,
                CreatedByUserId = actorUserId,
                CreatedAtUtc = now
            });

        account.UpdatedAtUtc = now;

        await using var dbTransaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M04.8.6.FamilySharedAccountOperationPosted",
                EntityType: "PersonalFinancialTransaction",
                EntityId: transactionId.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Właściciel lub współwłaściciel zaksięgował operację na wspólnym koncie rodziny. Kwota i opis nie są zapisywane w audycie."),
            cancellationToken);

        await dbTransaction.CommitAsync(cancellationToken);

        return transactionId;
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

    private async Task<IReadOnlyList<ChildIncomeReceiptRow>> GetChildIncomeReceiptRowsByPeriodAsync(
        Guid familyGroupId,
        string periodKey,
        CancellationToken cancellationToken)
    {
        var result = new List<ChildIncomeReceiptRow>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT Id, FamilyGroupId, RuleId, PeriodKey,
                           BeneficiaryPersonId, SourceType, SourceId,
                           ReceivedIntoAccountId, ReceivedIntoPersonId,
                           AmountMinor, ReceivedByUserId, ReceivedAtUtc, CreatedAtUtc
                    FROM FamilyIncomeReceipts
                    WHERE FamilyGroupId = $groupId
                      AND PeriodKey = $periodKey
                    ORDER BY ReceivedAtUtc;
                    """;

                AddParameter(command, "$groupId", familyGroupId);
                AddParameter(command, "$periodKey", periodKey);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    result.Add(ReadChildIncomeReceipt(reader));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<IReadOnlyList<ChildIncomeReceiptRow>> GetChildIncomeReceiptRowsByReceivedRangeAsync(
        Guid familyGroupId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var result = new List<ChildIncomeReceiptRow>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT Id, FamilyGroupId, RuleId, PeriodKey,
                           BeneficiaryPersonId, SourceType, SourceId,
                           ReceivedIntoAccountId, ReceivedIntoPersonId,
                           AmountMinor, ReceivedByUserId, ReceivedAtUtc, CreatedAtUtc
                    FROM FamilyIncomeReceipts
                    WHERE FamilyGroupId = $groupId
                      AND ReceivedAtUtc >= $fromUtc
                      AND ReceivedAtUtc < $toUtc
                    ORDER BY ReceivedAtUtc;
                    """;

                AddParameter(command, "$groupId", familyGroupId);
                AddParameter(command, "$fromUtc", fromUtc);
                AddParameter(command, "$toUtc", toUtc);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    result.Add(ReadChildIncomeReceipt(reader));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<ChildIncomeReceiptRow?> GetChildIncomeReceiptByRulePeriodAsync(
        Guid ruleId,
        string periodKey,
        CancellationToken cancellationToken)
    {
        ChildIncomeReceiptRow? result = null;

        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT Id, FamilyGroupId, RuleId, PeriodKey,
                           BeneficiaryPersonId, SourceType, SourceId,
                           ReceivedIntoAccountId, ReceivedIntoPersonId,
                           AmountMinor, ReceivedByUserId, ReceivedAtUtc, CreatedAtUtc
                    FROM FamilyIncomeReceipts
                    WHERE RuleId = $ruleId
                      AND PeriodKey = $periodKey
                    LIMIT 1;
                    """;

                AddParameter(command, "$ruleId", ruleId);
                AddParameter(command, "$periodKey", periodKey);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(cancellationToken);

                if (await reader.ReadAsync(cancellationToken))
                {
                    result = ReadChildIncomeReceipt(reader);
                }
            },
            cancellationToken);

        return result;
    }

    private static ChildIncomeReceiptRow ReadChildIncomeReceipt(
        DbDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetString(3),
            reader.GetGuid(4),
            reader.GetString(5),
            reader.GetGuid(6),
            reader.GetGuid(7),
            reader.IsDBNull(8) ? null : reader.GetGuid(8),
            reader.GetInt64(9),
            reader.GetGuid(10),
            ReadDateTime(reader, 11),
            ReadDateTime(reader, 12));

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



    private async Task<IReadOnlyList<FamilyChildHouseholdContributionSummary>>
        BuildChildHouseholdContributionSummariesAsync(
            Guid householdId,
            IReadOnlyList<FamilyMemberRow> familyMembers,
            int year,
            int month,
            CancellationToken cancellationToken)
    {
        var childPeople =
            familyMembers
                .Where(x =>
                    x.FamilyRoleCode == FamilyRoles.Child)
                .ToDictionary(
                    x => x.PersonId,
                    x => x.DisplayName);

        if (childPeople.Count == 0)
        {
            return [];
        }

        var childPersonIds =
            childPeople.Keys.ToArray();

        var householdMemberships =
            await dbContext.HouseholdMembers
                .AsNoTracking()
                .Where(x =>
                    x.HouseholdId == householdId &&
                    x.IsActive &&
                    childPersonIds.Contains(x.PersonId))
                .Select(x => new
                {
                    x.Id,
                    x.PersonId
                })
                .ToArrayAsync(
                    cancellationToken);

        if (householdMemberships.Length == 0)
        {
            return [];
        }

        var membershipToPerson =
            householdMemberships.ToDictionary(
                x => x.Id,
                x => x.PersonId);

        var householdMemberIds =
            membershipToPerson.Keys.ToArray();

        var ruleRows =
            await (
                from rule in dbContext.HouseholdContributionRules
                    .AsNoTracking()
                join targetAccount in dbContext.HouseholdAccounts
                    .AsNoTracking()
                    on rule.TargetHouseholdAccountId equals targetAccount.Id
                where
                    rule.HouseholdId == householdId &&
                    householdMemberIds.Contains(rule.HouseholdMemberId) &&
                    rule.ModeCode == HouseholdContributionModes.FixedAmount &&
                    !rule.IncomeRuleId.HasValue &&
                    rule.FixedAmountMinor.HasValue
                orderby
                    rule.IsActive descending,
                    rule.ValidFromUtc descending
                select new
                {
                    Rule = rule,
                    TargetAccount = targetAccount
                })
                .ToArrayAsync(
                    cancellationToken);

        if (ruleRows.Length == 0)
        {
            return [];
        }

        var periodKey =
            $"{year:D4}-{month:D2}";

        var ruleIds =
            ruleRows
                .Select(x => x.Rule.Id)
                .ToArray();

        var obligations =
            await dbContext.HouseholdContributionObligations
                .AsNoTracking()
                .Where(x =>
                    ruleIds.Contains(x.ContributionRuleId) &&
                    x.PeriodKey == periodKey)
                .ToArrayAsync(
                    cancellationToken);

        var obligationByRule =
            obligations
                .GroupBy(x => x.ContributionRuleId)
                .ToDictionary(
                    x => x.Key,
                    x => x
                        .OrderByDescending(y => y.UpdatedAtUtc)
                        .First());

        var todayUtc =
            DateTime.UtcNow.Date;

        var currentMonth =
            new DateTime(
                todayUtc.Year,
                todayUtc.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var selectedMonth =
            new DateTime(
                year,
                month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

        var result =
            new List<FamilyChildHouseholdContributionSummary>();

        foreach (var row in ruleRows)
        {
            var rule = row.Rule;
            var personId =
                membershipToPerson[rule.HouseholdMemberId];

            obligationByRule.TryGetValue(
                rule.Id,
                out var obligation);

            var validFromMonth =
                new DateTime(
                    rule.ValidFromUtc.Year,
                    rule.ValidFromUtc.Month,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc);

            DateTime? validToMonth =
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

            var appliesByMonth =
                selectedMonth >= validFromMonth &&
                (!validToMonth.HasValue ||
                 selectedMonth <= validToMonth.Value);

            // Zawsze pokazujemy aktualną regułę dziecka. Historyczną wersję
            // pokazujemy tylko wtedy, gdy ma zobowiązanie w wybranym miesiącu.
            if (obligation is null &&
                !rule.IsActive &&
                !appliesByMonth)
            {
                continue;
            }

            if (obligation is not null)
            {
                var outstandingMinor =
                    Math.Max(
                        0,
                        obligation.AmountMinor -
                        obligation.PaidAmountMinor);

                var statusCode =
                    ResolveChildContributionStatus(
                        obligation,
                        todayUtc);

                var canPay =
                    outstandingMinor > 0 &&
                    statusCode !=
                        HouseholdContributionStatuses.Cancelled &&
                    statusCode !=
                        HouseholdContributionStatuses.Corrected &&
                    selectedMonth <= currentMonth;

                result.Add(
                    new FamilyChildHouseholdContributionSummary(
                        obligation.Id,
                        rule.Id,
                        personId,
                        childPeople.GetValueOrDefault(
                            personId,
                            "Dziecko"),
                        obligation.PeriodKey,
                        HouseholdFinanceMoney.FromMinorUnits(
                            obligation.AmountMinor),
                        HouseholdFinanceMoney.FromMinorUnits(
                            obligation.PaidAmountMinor),
                        HouseholdFinanceMoney.FromMinorUnits(
                            outstandingMinor),
                        obligation.DueDateUtc,
                        statusCode,
                        HouseholdContributionStatuses.GetNamePl(
                            statusCode),
                        row.TargetAccount.Id,
                        row.TargetAccount.Name,
                        row.TargetAccount.CurrencyCode,
                        canPay,
                        null));

                continue;
            }

            var dueDay =
                Math.Clamp(
                    rule.DueOffsetDays,
                    1,
                    28);

            var dueDateUtc =
                new DateTime(
                    year,
                    month,
                    dueDay,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc);

            string statusName;
            string availabilityNote;

            if (selectedMonth < validFromMonth)
            {
                statusName = "Jeszcze nie obowiązuje";
                availabilityNote =
                    $"Reguła obowiązuje od {validFromMonth:MM.yyyy}.";
            }
            else if (validToMonth.HasValue &&
                     selectedMonth > validToMonth.Value)
            {
                statusName = "Poza okresem";
                availabilityNote =
                    $"Reguła obowiązywała do {validToMonth.Value:MM.yyyy}.";
            }
            else if (dueDateUtc.Date < rule.ValidFromUtc.Date)
            {
                statusName = "Od następnego miesiąca";
                availabilityNote =
                    "Regułę ustawiono po terminie składki w tym miesiącu. Pierwsze zobowiązanie pojawi się w następnym miesiącu.";
            }
            else if (!rule.IsActive)
            {
                statusName = "Reguła zakończona";
                availabilityNote =
                    "Ta wersja reguły nie jest już aktywna.";
            }
            else
            {
                statusName = "Reguła aktywna";
                availabilityNote =
                    "Składka jest ustawiona, ale dla wybranego miesiąca nie ma jeszcze zobowiązania do przekazania.";
            }

            var amountMinor =
                rule.FixedAmountMinor ?? 0;

            result.Add(
                new FamilyChildHouseholdContributionSummary(
                    null,
                    rule.Id,
                    personId,
                    childPeople.GetValueOrDefault(
                        personId,
                        "Dziecko"),
                    periodKey,
                    HouseholdFinanceMoney.FromMinorUnits(
                        amountMinor),
                    0m,
                    HouseholdFinanceMoney.FromMinorUnits(
                        amountMinor),
                    dueDateUtc,
                    "RuleOnly",
                    statusName,
                    row.TargetAccount.Id,
                    row.TargetAccount.Name,
                    row.TargetAccount.CurrencyCode,
                    false,
                    availabilityNote));
        }

        return result
            .OrderBy(x => x.ChildDisplayName)
            .ThenBy(x => x.DueDateUtc)
            .ToArray();
    }

    private static string ResolveChildContributionStatus(
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
            return obligation.DueDateUtc.Date < todayUtc
                ? HouseholdContributionStatuses.Overdue
                : HouseholdContributionStatuses.PartiallyPaid;
        }

        return obligation.DueDateUtc.Date < todayUtc
            ? HouseholdContributionStatuses.Overdue
            : HouseholdContributionStatuses.Pending;
    }


    private async Task<IReadOnlyList<FamilySharedAccountSummary>>
        BuildSharedAccountSummariesForGroupAsync(
            Guid familyGroupId,
            Guid currentPersonId,
            CancellationToken cancellationToken)
    {
        var rows = await GetSharedAccountRowsForGroupAsync(
            familyGroupId,
            cancellationToken);

        return await BuildSharedAccountSummariesAsync(
            rows,
            currentPersonId,
            cancellationToken);
    }

    private async Task<IReadOnlyList<FamilySharedAccountSummary>>
        BuildSharedAccountSummariesAsync(
            IReadOnlyList<SharedAccountRow> rows,
            Guid currentPersonId,
            CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var accountIds = rows
            .Select(x => x.PersonalAccountId)
            .Distinct()
            .ToArray();

        var accounts = await dbContext.PersonalFinancialAccounts
            .AsNoTracking()
            .Where(x => accountIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                cancellationToken);

        var balances = await dbContext.PersonalFinancialTransactions
            .AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId))
            .GroupBy(x => x.AccountId)
            .Select(x => new
            {
                AccountId = x.Key,
                BalanceMinor = x.Sum(y => y.AmountMinor)
            })
            .ToDictionaryAsync(
                x => x.AccountId,
                x => x.BalanceMinor,
                cancellationToken);

        var personIds = rows
            .SelectMany(x => new[]
            {
                x.OwnerPersonId,
                x.CoOwnerPersonId
            })
            .Distinct()
            .ToArray();

        var people = await dbContext.People
            .AsNoTracking()
            .Where(x => personIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => BuildDisplayName(
                    x.DisplayName,
                    x.FirstName,
                    x.LastName),
                cancellationToken);

        var result = new List<FamilySharedAccountSummary>();

        foreach (var row in rows)
        {
            if (!accounts.TryGetValue(
                    row.PersonalAccountId,
                    out var account))
            {
                continue;
            }

            var roleCode = row.OwnerPersonId == currentPersonId
                ? FamilySharedAccountRoles.Owner
                : row.CoOwnerPersonId == currentPersonId
                    ? FamilySharedAccountRoles.CoOwner
                    : FamilySharedAccountRoles.Viewer;

            result.Add(
                new FamilySharedAccountSummary(
                    row.Id,
                    row.FamilyGroupId,
                    row.FamilyGroupName,
                    account.Id,
                    account.Name,
                    account.AccountTypeCode,
                    PersonalAccountTypes.GetNamePl(
                        account.AccountTypeCode),
                    account.CurrencyCode,
                    PersonalFinanceMoney.FromMinorUnits(
                        balances.GetValueOrDefault(account.Id)),
                    row.OwnerPersonId,
                    people.GetValueOrDefault(
                        row.OwnerPersonId,
                        "Właściciel"),
                    row.CoOwnerPersonId,
                    people.GetValueOrDefault(
                        row.CoOwnerPersonId,
                        "Współwłaściciel"),
                    roleCode,
                    FamilySharedAccountRoles.GetNamePl(roleCode),
                    account.IsActive && row.ClosedAtUtc is null));
        }

        return result
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.AccountName)
            .ToArray();
    }

    private async Task<IReadOnlyList<SharedAccountRow>>
        GetSharedAccountRowsForGroupAsync(
            Guid familyGroupId,
            CancellationToken cancellationToken)
    {
        var result = new List<SharedAccountRow>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT s.Id, s.FamilyGroupId, g.Name, s.PersonalAccountId,
                           s.OwnerPersonId, s.CoOwnerPersonId, s.ClosedAtUtc
                    FROM FamilySharedAccounts s
                    INNER JOIN FamilyGroups g
                        ON g.Id = s.FamilyGroupId
                    WHERE s.FamilyGroupId = $familyGroupId
                      AND g.IsActive = 1
                    ORDER BY s.CreatedAtUtc, s.Id;
                    """;

                AddParameter(
                    command,
                    "$familyGroupId",
                    familyGroupId);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    result.Add(
                        ReadSharedAccountRow(reader));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<IReadOnlyList<SharedAccountRow>>
        GetSharedAccountRowsForPersonAsync(
            Guid personId,
            CancellationToken cancellationToken)
    {
        var result = new List<SharedAccountRow>();

        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT DISTINCT
                           s.Id, s.FamilyGroupId, g.Name, s.PersonalAccountId,
                           s.OwnerPersonId, s.CoOwnerPersonId, s.ClosedAtUtc
                    FROM FamilySharedAccounts s
                    INNER JOIN FamilyGroups g
                        ON g.Id = s.FamilyGroupId
                    INNER JOIN FamilyMembers m
                        ON m.FamilyGroupId = s.FamilyGroupId
                       AND m.PersonId = $personId
                       AND m.ValidToUtc IS NULL
                    WHERE (s.OwnerPersonId = $personId
                           OR s.CoOwnerPersonId = $personId)
                      AND g.IsActive = 1
                    ORDER BY g.Name, s.CreatedAtUtc, s.Id;
                    """;

                AddParameter(
                    command,
                    "$personId",
                    personId);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    result.Add(
                        ReadSharedAccountRow(reader));
                }
            },
            cancellationToken);

        return result;
    }

    private async Task<SharedAccountRow?>
        GetSharedAccountRowForPersonAsync(
            Guid sharedAccountId,
            Guid personId,
            CancellationToken cancellationToken)
    {
        SharedAccountRow? result = null;

        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT s.Id, s.FamilyGroupId, g.Name, s.PersonalAccountId,
                           s.OwnerPersonId, s.CoOwnerPersonId, s.ClosedAtUtc
                    FROM FamilySharedAccounts s
                    INNER JOIN FamilyGroups g
                        ON g.Id = s.FamilyGroupId
                    INNER JOIN FamilyMembers m
                        ON m.FamilyGroupId = s.FamilyGroupId
                       AND m.PersonId = $personId
                       AND m.ValidToUtc IS NULL
                    WHERE s.Id = $sharedAccountId
                      AND (s.OwnerPersonId = $personId
                           OR s.CoOwnerPersonId = $personId)
                      AND g.IsActive = 1
                    LIMIT 1;
                    """;

                AddParameter(
                    command,
                    "$sharedAccountId",
                    sharedAccountId);
                AddParameter(
                    command,
                    "$personId",
                    personId);
                AttachCurrentTransaction(command);

                await using var reader =
                    await command.ExecuteReaderAsync(
                        cancellationToken);

                if (await reader.ReadAsync(cancellationToken))
                {
                    result =
                        ReadSharedAccountRow(reader);
                }
            },
            cancellationToken);

        return result;
    }

    private static SharedAccountRow ReadSharedAccountRow(
        DbDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetGuid(3),
            reader.GetGuid(4),
            reader.GetGuid(5),
            reader.IsDBNull(6)
                ? (DateTime?)null
                : ReadDateTime(reader, 6));

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

    private static DateTime BuildPlannedDate(
        int year,
        int month,
        int dueDay)
    {
        var day = Math.Min(
            Math.Max(dueDay, 1),
            DateTime.DaysInMonth(year, month));

        return new DateTime(
            year,
            month,
            day,
            0,
            0,
            0,
            DateTimeKind.Utc);
    }

    private static DateTime NormalizeUtcDate(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

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

    private sealed record SharedAccountRow(
        Guid Id,
        Guid FamilyGroupId,
        string FamilyGroupName,
        Guid PersonalAccountId,
        Guid OwnerPersonId,
        Guid CoOwnerPersonId,
        DateTime? ClosedAtUtc);

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

    private sealed record ChildIncomeReceiptRow(
        Guid Id,
        Guid FamilyGroupId,
        Guid RuleId,
        string PeriodKey,
        Guid BeneficiaryPersonId,
        string SourceType,
        Guid SourceId,
        Guid ReceivedIntoAccountId,
        Guid? ReceivedIntoPersonId,
        long AmountMinor,
        Guid ReceivedByUserId,
        DateTime ReceivedAtUtc,
        DateTime CreatedAtUtc);

    private sealed record ParameterValue(
        string Name,
        object? Value);
}
