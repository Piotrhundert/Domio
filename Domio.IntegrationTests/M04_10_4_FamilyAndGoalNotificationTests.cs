using Domio.Application.Authentication;
using Domio.Application.FamilyFinance;
using Domio.Application.FinancialGoals;
using Domio.Application.HouseholdFinance;
using Domio.Domain.FamilyFinance;
using Domio.Domain.FinancialGoals;
using Domio.Domain.HouseholdFinance;
using Domio.Domain.Users;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.FamilyFinance;
using Domio.Infrastructure.FinancialGoals;
using Domio.Infrastructure.HouseholdFinance;
using Domio.Infrastructure.Notifications;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M04_10_4_FamilyAndGoalNotificationTests
{
    [Fact]
    public async Task Scanner_should_create_family_child_and_goal_notifications_without_duplicates()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"domio-m04-10-4-{Guid.NewGuid():N}");

        Directory.CreateDirectory(
            root);

        var databasePath =
            Path.Combine(
                root,
                "domio-test.db");

        try
        {
            var options =
                new DbContextOptionsBuilder<DomioDbContext>()
                    .UseSqlite(
                        $"Data Source={databasePath};Foreign Keys=True;Pooling=False")
                    .Options;

            await using var dbContext =
                new DomioDbContext(
                    options);

            await dbContext.Database.MigrateAsync();

            var auditService =
                new AuditService(
                    dbContext);

            var authenticationService =
                new AccountAuthenticationService(
                    dbContext,
                    auditService);

            var administrator =
                await authenticationService
                    .InitializeFirstAdministratorAsync(
                        new FirstAdministratorSetupRequest(
                            "Jan",
                            "Administrator",
                            "admin",
                            "DomioTest123"),
                        Guid.NewGuid()
                            .ToString("N"));

            var householdService =
                new HouseholdFinanceService(
                    dbContext,
                    auditService);

            await householdService.CreateAccountAsync(
                new CreateHouseholdAccountRequest(
                    "Konto domu",
                    HouseholdAccountTypes.Bank,
                    "PLN",
                    5000m),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var familyService =
                new FamilyFinanceService(
                    dbContext,
                    auditService);

            var familyGroupId =
                await familyService.CreateGroupAsync(
                    new CreateFamilyGroupRequest(
                        "Rodzina testowa"),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var child =
                new Person
                {
                    Id = Guid.NewGuid(),
                    FirstName = "Tymek",
                    LastName = "Testowy",
                    DisplayName = "Tymek",
                    PersonTypeCode = PersonTypes.Child,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

            dbContext.People.Add(
                child);

            await dbContext.SaveChangesAsync();

            await familyService.AddMemberAsync(
                new AddFamilyMemberRequest(
                    familyGroupId,
                    child.Id,
                    FamilyRoles.Child),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            await familyService.CreateChildIncomeAsync(
                new CreateFamilyChildIncomeRequest(
                    familyGroupId,
                    child.Id,
                    FamilyIncomeKinds.ChildBenefit800Plus,
                    null,
                    800m,
                    FamilyRecurringFrequencies.Monthly,
                    Math.Min(
                        DateTime.UtcNow.Day,
                        28),
                    new DateTime(
                        DateTime.UtcNow.Year,
                        DateTime.UtcNow.Month,
                        1,
                        0,
                        0,
                        0,
                        DateTimeKind.Utc),
                    null),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var goalService =
                new FinancialGoalService(
                    dbContext,
                    auditService);

            var goalId =
                await goalService.CreateAsync(
                    new CreateFinancialGoalRequest(
                        FinancialGoalScopes.Family,
                        familyGroupId,
                        "Wakacje",
                        FinancialGoalCategories.Travel,
                        1000m,
                        DateTime.UtcNow.Date.AddDays(7),
                        null,
                        500m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var notificationService =
                new NotificationService(
                    dbContext);

            var scanner =
                new FamilyAndGoalNotificationScanService(
                    dbContext,
                    notificationService);

            await scanner.ScanAsync(
                administrator.UserId);

            await scanner.ScanAsync(
                administrator.UserId);

            var overview =
                await notificationService.GetOverviewAsync(
                    administrator.UserId);

            Assert.Single(
                overview.ActivityHistory.Where(x =>
                    x.EventCode ==
                    "M04.10.4.ChildIncomeCreated"));

            Assert.Contains(
                overview.ActivityHistory,
                x =>
                    x.EventCode ==
                    "M04.10.4.FamilyMemberAdded");

            Assert.Contains(
                overview.ActivityHistory,
                x =>
                    x.EventCode ==
                    "M04.10.4.FinancialGoalCreated");

            Assert.Contains(
                overview.Inbox,
                x =>
                    x.EventCode ==
                    "M04.10.4.FinancialGoalMilestone" &&
                    x.Title.Contains(
                        "50%",
                        StringComparison.Ordinal));

            Assert.Contains(
                overview.Inbox,
                x =>
                    x.EventCode ==
                    "M04.10.4.FinancialGoalUpcoming");

            await goalService.AddContributionAsync(
                new AddFinancialGoalContributionRequest(
                    goalId,
                    250m,
                    DateTime.UtcNow,
                    "Druga wpłata"),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            await scanner.ScanAsync(
                administrator.UserId);

            overview =
                await notificationService.GetOverviewAsync(
                    administrator.UserId);

            Assert.Contains(
                overview.Inbox,
                x =>
                    x.EventCode ==
                    "M04.10.4.FinancialGoalMilestone" &&
                    x.Title.Contains(
                        "75%",
                        StringComparison.Ordinal));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }
}
