using Domio.Application.Authentication;
using Domio.Application.HouseholdFinance;
using Domio.Application.PersonalFinance;
using Domio.Application.Users;
using Domio.Domain.HouseholdFinance;
using Domio.Domain.PersonalFinance;
using Domio.Domain.Users;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.HouseholdFinance;
using Domio.Infrastructure.Persistence;
using Domio.Infrastructure.PersonalFinance;
using Domio.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M04_4_HouseholdContributionPaymentApprovalTests
{
    [Fact]
    public async Task Member_should_submit_payment_and_admin_approval_should_book_both_sides_atomically()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"domio-m04-4-payment-{Guid.NewGuid():N}");

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

            await dbContext.Database
                .MigrateAsync();

            var schemaVersion =
                await dbContext.SchemaVersions
                    .AsNoTracking()
                    .Where(x =>
                        x.Id == 1)
                    .Select(x =>
                        x.Version)
                    .SingleAsync();

            Assert.True(
                schemaVersion >= 14,
                $"Oczekiwano schemaVersion >= 14, otrzymano {schemaVersion}.");

            Assert.Empty(
                await dbContext.Database
                    .GetPendingMigrationsAsync());

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
                        Guid.NewGuid().ToString("N"));

            var userManagement =
                new UserManagementService(
                    dbContext,
                    auditService);

            var memberUserId =
                await userManagement.CreateUserAsync(
                    new CreateUserRequest(
                        null,
                        "Anna",
                        "Domownik",
                        null,
                        null,
                        "anna@example.test",
                        "DomioAnna123",
                        SystemRoles.HouseholdMemberId),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var householdService =
                new HouseholdFinanceService(
                    dbContext,
                    auditService);

            var householdAccountId =
                await householdService.CreateAccountAsync(
                    new CreateHouseholdAccountRequest(
                        "Konto Domowe",
                        HouseholdAccountTypes.Bank,
                        "PLN",
                        0m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var personalService =
                new PersonalFinanceService(
                    dbContext,
                    auditService);

            var memberPrivateAccountId =
                await personalService.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Konto prywatne Anny",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        1000m),
                    memberUserId,
                    Guid.NewGuid().ToString("N"));

            var now =
                DateTime.UtcNow;

            var salaryStart =
                new DateTime(
                    now.Year,
                    now.Month,
                    Math.Min(
                        10,
                        DateTime.DaysInMonth(
                            now.Year,
                            now.Month)),
                    0,
                    0,
                    0,
                    DateTimeKind.Utc);

            var salaryRuleId =
                await personalService.CreateOwnRecurringRuleAsync(
                    new CreatePersonalRecurringRuleRequest(
                        memberPrivateAccountId,
                        PersonalTransactionKinds.Income,
                        "Wynagrodzenie",
                        3000m,
                        PersonalRecurringFrequencies.Monthly,
                        PersonalFinanceCategories.Salary,
                        "Firma Test",
                        salaryStart,
                        null),
                    memberUserId,
                    Guid.NewGuid().ToString("N"));

            var salaryOccurrence =
                (await personalService.GetOwnOverviewAsync(
                    memberUserId))
                    .RecurringOccurrences
                    .Where(x =>
                        x.RuleId ==
                            salaryRuleId &&
                        x.PlannedDateUtc.Year ==
                            now.Year &&
                        x.PlannedDateUtc.Month ==
                            now.Month)
                    .Single();

            var contributionBatch =
                await householdService.CreateContributionRuleAsync(
                    new CreateHouseholdContributionRuleRequest(
                        SystemRoles.HouseholdMemberId,
                        HouseholdContributionModes.FixedAmount,
                        300m,
                        null,
                        7,
                        householdAccountId,
                        new DateTime(
                            now.Year,
                            now.Month,
                            1,
                            0,
                            0,
                            0,
                            DateTimeKind.Utc),
                        null,
                        3),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            Assert.Equal(
                1,
                contributionBatch.RulesCreated);

            var memberContributions =
                await householdService.GetContributionOverviewAsync(
                    memberUserId);

            Assert.NotNull(
                memberContributions);

            var obligation =
                memberContributions!.Obligations
                    .Single(x =>
                        x.PeriodKey ==
                            salaryOccurrence.PeriodKey);

            Assert.True(
                obligation.IsOwn);

            Assert.Equal(
                300m,
                obligation.OutstandingAmount);

            var paymentForm =
                await householdService.GetContributionPaymentFormAsync(
                    obligation.ObligationId,
                    memberUserId);

            Assert.NotNull(
                paymentForm);

            Assert.Equal(
                1000m,
                paymentForm!.SourceAccounts
                    .Single(x =>
                        x.AccountId ==
                            memberPrivateAccountId)
                    .Balance);

            var paymentRequestId =
                await householdService.SubmitContributionPaymentAsync(
                    new SubmitHouseholdContributionPaymentRequest(
                        obligation.ObligationId,
                        memberPrivateAccountId,
                        200m),
                    memberUserId,
                    Guid.NewGuid().ToString("N"));

            var memberBalanceAfterSubmit =
                (await personalService.GetOwnOverviewAsync(
                    memberUserId))
                    .Accounts
                    .Single(x =>
                        x.AccountId ==
                            memberPrivateAccountId)
                    .Balance;

            var householdBalanceAfterSubmit =
                (await householdService.GetOverviewAsync(
                    administrator.UserId))!
                    .Accounts
                    .Single(x =>
                        x.AccountId ==
                            householdAccountId)
                    .Balance;

            Assert.Equal(
                1000m,
                memberBalanceAfterSubmit);

            Assert.Equal(
                0m,
                householdBalanceAfterSubmit);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    householdService.SubmitContributionPaymentAsync(
                        new SubmitHouseholdContributionPaymentRequest(
                            obligation.ObligationId,
                            memberPrivateAccountId,
                            50m),
                        memberUserId,
                        Guid.NewGuid().ToString("N")));

            var memberViewWithPending =
                await householdService.GetContributionOverviewAsync(
                    memberUserId);

            var memberPayment =
                memberViewWithPending!.PaymentRequests
                    .Single(x =>
                        x.PaymentRequestId ==
                            paymentRequestId);

            Assert.True(
                memberPayment.IsOwn);

            Assert.Equal(
                "Konto prywatne Anny",
                memberPayment.SourcePersonalAccountName);

            Assert.Equal(
                HouseholdContributionPaymentStatuses.Pending,
                memberPayment.StatusCode);

            var adminViewWithPending =
                await householdService.GetContributionOverviewAsync(
                    administrator.UserId);

            Assert.True(
                adminViewWithPending!.CanApprove);

            var adminPayment =
                adminViewWithPending.PaymentRequests
                    .Single(x =>
                        x.PaymentRequestId ==
                            paymentRequestId);

            Assert.False(
                adminPayment.IsOwn);

            Assert.Null(
                adminPayment.SourcePersonalAccountName);

            await householdService.ApproveContributionPaymentAsync(
                new ReviewHouseholdContributionPaymentRequest(
                    paymentRequestId,
                    null),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var memberBalanceAfterApproval =
                (await personalService.GetOwnOverviewAsync(
                    memberUserId))
                    .Accounts
                    .Single(x =>
                        x.AccountId ==
                            memberPrivateAccountId)
                    .Balance;

            var householdBalanceAfterApproval =
                (await householdService.GetOverviewAsync(
                    administrator.UserId))!
                    .Accounts
                    .Single(x =>
                        x.AccountId ==
                            householdAccountId)
                    .Balance;

            Assert.Equal(
                800m,
                memberBalanceAfterApproval);

            Assert.Equal(
                200m,
                householdBalanceAfterApproval);

            var obligationAfterPartial =
                await dbContext.HouseholdContributionObligations
                    .AsNoTracking()
                    .SingleAsync(x =>
                        x.Id ==
                            obligation.ObligationId);

            Assert.Equal(
                20000,
                obligationAfterPartial.PaidAmountMinor);

            Assert.Equal(
                HouseholdContributionStatuses.PartiallyPaid,
                obligationAfterPartial.StatusCode);

            var approvedRequest =
                await dbContext.HouseholdContributionPaymentRequests
                    .AsNoTracking()
                    .SingleAsync(x =>
                        x.Id ==
                            paymentRequestId);

            Assert.Equal(
                HouseholdContributionPaymentStatuses.Approved,
                approvedRequest.StatusCode);

            Assert.NotNull(
                approvedRequest.PersonalTransactionId);

            Assert.NotNull(
                approvedRequest.HouseholdEntryId);

            var privateDebit =
                await dbContext.PersonalFinancialTransactions
                    .AsNoTracking()
                    .SingleAsync(x =>
                        x.Id ==
                            approvedRequest.PersonalTransactionId);

            Assert.Equal(
                -20000,
                privateDebit.AmountMinor);

            Assert.Equal(
                PersonalFinanceCategories.HouseholdContribution,
                privateDebit.CategoryCode);

            var householdCredit =
                await dbContext.HouseholdEntries
                    .AsNoTracking()
                    .SingleAsync(x =>
                        x.Id ==
                            approvedRequest.HouseholdEntryId);

            Assert.Equal(
                20000,
                householdCredit.AmountMinor);

            Assert.Equal(
                HouseholdEntryTypes.MemberContribution,
                householdCredit.EntryTypeCode);

            Assert.Equal(
                "HouseholdContributionPayment",
                householdCredit.SourceType);

            var secondRequestId =
                await householdService.SubmitContributionPaymentAsync(
                    new SubmitHouseholdContributionPaymentRequest(
                        obligation.ObligationId,
                        memberPrivateAccountId,
                        100m),
                    memberUserId,
                    Guid.NewGuid().ToString("N"));

            await householdService.ApproveContributionPaymentAsync(
                new ReviewHouseholdContributionPaymentRequest(
                    secondRequestId,
                    "Pozostała część składki"),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var finalPersonalBalance =
                (await personalService.GetOwnOverviewAsync(
                    memberUserId))
                    .Accounts
                    .Single(x =>
                        x.AccountId ==
                            memberPrivateAccountId)
                    .Balance;

            var finalHouseholdBalance =
                (await householdService.GetOverviewAsync(
                    administrator.UserId))!
                    .Accounts
                    .Single(x =>
                        x.AccountId ==
                            householdAccountId)
                    .Balance;

            Assert.Equal(
                700m,
                finalPersonalBalance);

            Assert.Equal(
                300m,
                finalHouseholdBalance);

            var finalObligation =
                await dbContext.HouseholdContributionObligations
                    .AsNoTracking()
                    .SingleAsync(x =>
                        x.Id ==
                            obligation.ObligationId);

            Assert.Equal(
                30000,
                finalObligation.PaidAmountMinor);

            Assert.Equal(
                HouseholdContributionStatuses.Paid,
                finalObligation.StatusCode);

            var auditRows =
                await dbContext.AuditLogs
                    .AsNoTracking()
                    .Where(x =>
                        x.EventType.StartsWith(
                            "M04.4."))
                    .ToArrayAsync();

            Assert.Contains(
                auditRows,
                x =>
                    x.EventType ==
                    "M04.4.ContributionPaymentSubmitted");

            Assert.Contains(
                auditRows,
                x =>
                    x.EventType ==
                    "M04.4.ContributionPaymentApproved");

            Assert.All(
                auditRows,
                entry =>
                {
                    var auditText =
                        string.Join(
                            " ",
                            entry.Description,
                            entry.OldValuesJson,
                            entry.NewValuesJson);

                    Assert.DoesNotContain(
                        "200",
                        auditText);

                    Assert.DoesNotContain(
                        "Konto prywatne Anny",
                        auditText);
                });
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection
                .ClearAllPools();

            if (Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task Approval_should_recheck_balance_and_rejection_should_not_change_balances()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"domio-m04-4-recheck-{Guid.NewGuid():N}");

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

            await dbContext.Database
                .MigrateAsync();

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
                        Guid.NewGuid().ToString("N"));

            var userManagement =
                new UserManagementService(
                    dbContext,
                    auditService);

            var memberUserId =
                await userManagement.CreateUserAsync(
                    new CreateUserRequest(
                        null,
                        "Anna",
                        "Domownik",
                        null,
                        null,
                        "anna2@example.test",
                        "DomioAnna123",
                        SystemRoles.HouseholdMemberId),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var householdService =
                new HouseholdFinanceService(
                    dbContext,
                    auditService);

            var householdAccountId =
                await householdService.CreateAccountAsync(
                    new CreateHouseholdAccountRequest(
                        "Konto Domowe",
                        HouseholdAccountTypes.Bank,
                        "PLN",
                        0m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var personalService =
                new PersonalFinanceService(
                    dbContext,
                    auditService);

            var privateAccountId =
                await personalService.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Konto prywatne",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        100m),
                    memberUserId,
                    Guid.NewGuid().ToString("N"));

            var now =
                DateTime.UtcNow;

            var salaryRuleId =
                await personalService.CreateOwnRecurringRuleAsync(
                    new CreatePersonalRecurringRuleRequest(
                        privateAccountId,
                        PersonalTransactionKinds.Income,
                        "Wynagrodzenie",
                        2000m,
                        PersonalRecurringFrequencies.Monthly,
                        PersonalFinanceCategories.Salary,
                        "Firma Test",
                        new DateTime(
                            now.Year,
                            now.Month,
                            10,
                            0,
                            0,
                            0,
                            DateTimeKind.Utc),
                        null),
                    memberUserId,
                    Guid.NewGuid().ToString("N"));

            var salaryOccurrence =
                (await personalService.GetOwnOverviewAsync(
                    memberUserId))
                    .RecurringOccurrences
                    .Where(x =>
                        x.RuleId ==
                            salaryRuleId &&
                        x.PlannedDateUtc.Year ==
                            now.Year &&
                        x.PlannedDateUtc.Month ==
                            now.Month)
                    .Single();

            _ = await householdService.CreateContributionRuleAsync(
                new CreateHouseholdContributionRuleRequest(
                    SystemRoles.HouseholdMemberId,
                    HouseholdContributionModes.FixedAmount,
                    100m,
                    null,
                    7,
                    householdAccountId,
                    new DateTime(
                        now.Year,
                        now.Month,
                        1,
                        0,
                        0,
                        0,
                        DateTimeKind.Utc),
                    null,
                    3),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var obligation =
                (await householdService.GetContributionOverviewAsync(
                    memberUserId))!
                    .Obligations
                    .Single(x =>
                        x.PeriodKey ==
                            salaryOccurrence.PeriodKey);

            var paymentRequestId =
                await householdService.SubmitContributionPaymentAsync(
                    new SubmitHouseholdContributionPaymentRequest(
                        obligation.ObligationId,
                        privateAccountId,
                        100m),
                    memberUserId,
                    Guid.NewGuid().ToString("N"));

            await personalService.PostOwnOperationAsync(
                new PostPersonalOperationRequest(
                    privateAccountId,
                    PersonalTransactionKinds.Expense,
                    100m,
                    DateTime.UtcNow,
                    "Wydatek po wysłaniu wpłaty",
                    PersonalFinanceCategories.OtherExpense),
                memberUserId,
                Guid.NewGuid().ToString("N"));

            var approvalError =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () =>
                        householdService.ApproveContributionPaymentAsync(
                            new ReviewHouseholdContributionPaymentRequest(
                                paymentRequestId,
                                null),
                            administrator.UserId,
                            Guid.NewGuid().ToString("N")));

            Assert.Contains(
                "nie ma już wystarczających środków",
                approvalError.Message);

            var requestStillPending =
                await dbContext.HouseholdContributionPaymentRequests
                    .AsNoTracking()
                    .SingleAsync(x =>
                        x.Id ==
                            paymentRequestId);

            Assert.Equal(
                HouseholdContributionPaymentStatuses.Pending,
                requestStillPending.StatusCode);

            Assert.Null(
                requestStillPending.PersonalTransactionId);

            Assert.Null(
                requestStillPending.HouseholdEntryId);

            Assert.Equal(
                0m,
                (await householdService.GetOverviewAsync(
                    administrator.UserId))!
                    .Accounts
                    .Single(x =>
                        x.AccountId ==
                            householdAccountId)
                    .Balance);

            await householdService.RejectContributionPaymentAsync(
                new ReviewHouseholdContributionPaymentRequest(
                    paymentRequestId,
                    "Brak środków"),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var rejected =
                await dbContext.HouseholdContributionPaymentRequests
                    .AsNoTracking()
                    .SingleAsync(x =>
                        x.Id ==
                            paymentRequestId);

            Assert.Equal(
                HouseholdContributionPaymentStatuses.Rejected,
                rejected.StatusCode);

            Assert.Equal(
                "Brak środków",
                rejected.ReviewNote);

            Assert.Equal(
                0,
                await dbContext.HouseholdEntries
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.SourceType ==
                            "HouseholdContributionPayment"));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection
                .ClearAllPools();

            if (Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }
}
