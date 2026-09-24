using Domio.Application.Authentication;
using Domio.Domain.HouseholdFinance;
using Domio.Domain.Notifications;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.Notifications;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M04_10_2_HouseholdNotificationsTests
{
    [Fact]
    public async Task Household_notifications_should_cover_contribution_and_member_obligation_deadlines_without_duplicates()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m04-10-2-notifications-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "domio-test.db");

        try
        {
            var options = new DbContextOptionsBuilder<DomioDbContext>()
                .UseSqlite($"Data Source={databasePath};Foreign Keys=True;Pooling=False")
                .Options;

            await using var dbContext = new DomioDbContext(options);
            await dbContext.Database.MigrateAsync();

            var schemaVersion = await dbContext.SchemaVersions
                .AsNoTracking()
                .Where(x => x.Id == 1)
                .Select(x => x.Version)
                .SingleAsync();

            Assert.InRange(schemaVersion, 26, int.MaxValue);
            Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync());

            var auditService = new AuditService(dbContext);
            var authenticationService = new AccountAuthenticationService(
                dbContext,
                auditService);

            var administrator = await authenticationService.InitializeFirstAdministratorAsync(
                new FirstAdministratorSetupRequest(
                    "Jan",
                    "Administrator",
                    "admin",
                    "DomioTest123"),
                Guid.NewGuid().ToString("N"));

            var personId = await dbContext.UserAccounts
                .AsNoTracking()
                .Where(x => x.Id == administrator.UserId)
                .Select(x => x.PersonId)
                .SingleAsync();

            var now = DateTime.UtcNow;
            var householdId = Guid.NewGuid();
            var membershipId = Guid.NewGuid();
            var householdAccountId = Guid.NewGuid();

            dbContext.Households.Add(
                new Household
                {
                    Id = householdId,
                    Name = "Dom testowy",
                    CurrencyCode = "PLN",
                    IsActive = true,
                    CreatedByUserId = administrator.UserId,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });

            dbContext.HouseholdMembers.Add(
                new HouseholdMember
                {
                    Id = membershipId,
                    HouseholdId = householdId,
                    PersonId = personId,
                    IsActive = true,
                    JoinedAtUtc = now
                });

            dbContext.HouseholdAccounts.Add(
                new HouseholdAccount
                {
                    Id = householdAccountId,
                    HouseholdId = householdId,
                    Name = "Konto domu",
                    AccountTypeCode = HouseholdAccountTypes.Bank,
                    CurrencyCode = "PLN",
                    IsActive = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });

            var contributionRuleId = Guid.NewGuid();
            dbContext.HouseholdContributionRules.Add(
                new HouseholdContributionRule
                {
                    Id = contributionRuleId,
                    HouseholdId = householdId,
                    HouseholdMemberId = membershipId,
                    ModeCode = HouseholdContributionModes.FixedAmount,
                    FixedAmountMinor = 50000,
                    DueOffsetDays = 2,
                    TargetHouseholdAccountId = householdAccountId,
                    ValidFromUtc = now.Date,
                    ReminderDays = 7,
                    IsActive = true,
                    CreatedByUserId = administrator.UserId,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });

            var contributionObligationId = Guid.NewGuid();
            dbContext.HouseholdContributionObligations.Add(
                new HouseholdContributionObligation
                {
                    Id = contributionObligationId,
                    ContributionRuleId = contributionRuleId,
                    HouseholdId = householdId,
                    HouseholdMemberId = membershipId,
                    TargetHouseholdAccountId = householdAccountId,
                    PeriodKey = $"{now.Year:D4}-{now.Month:D2}",
                    AmountMinor = 50000,
                    PaidAmountMinor = 0,
                    DueDateUtc = now.Date.AddDays(2),
                    StatusCode = HouseholdContributionStatuses.Pending,
                    ModeCode = HouseholdContributionModes.FixedAmount,
                    FixedAmountMinorSnapshot = 50000,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });

            var memberObligationId = Guid.NewGuid();
            dbContext.HouseholdMemberObligations.Add(
                new HouseholdMemberObligation
                {
                    Id = memberObligationId,
                    HouseholdId = householdId,
                    HouseholdMemberId = membershipId,
                    SourceTypeCode = HouseholdMemberObligationSourceTypes.OtherCost,
                    Description = "Dopłata testowa",
                    AmountMinor = 12000,
                    PaidAmountMinor = 0,
                    DueDateUtc = now.Date.AddDays(3),
                    StatusCode = HouseholdMemberObligationStatuses.Pending,
                    TargetHouseholdAccountId = householdAccountId,
                    CreatedByUserId = administrator.UserId,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });

            await dbContext.SaveChangesAsync();

            var notificationService = new NotificationService(dbContext);

            var first = await notificationService.GetOverviewAsync(
                administrator.UserId);

            Assert.Contains(
                first.Inbox,
                x =>
                    x.EventCode == "M04.10.2.ContributionObligationCreated" &&
                    x.SourceId == contributionObligationId.ToString());

            Assert.Contains(
                first.Inbox,
                x =>
                    x.EventCode == "M04.10.2.ContributionUpcoming" &&
                    x.SourceId == contributionObligationId.ToString());

            Assert.Contains(
                first.Inbox,
                x =>
                    x.EventCode == "M04.10.2.MemberObligationCreated" &&
                    x.SourceId == memberObligationId.ToString());

            Assert.Contains(
                first.Inbox,
                x =>
                    x.EventCode == "M04.10.2.MemberObligationUpcoming" &&
                    x.SourceId == memberObligationId.ToString());

            var countBefore = first.ActivityHistory.Count;

            var second = await notificationService.GetOverviewAsync(
                administrator.UserId);

            Assert.Equal(countBefore, second.ActivityHistory.Count);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
