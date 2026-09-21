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

public sealed class M04_7_HouseholdMemberObligationTests
{
    [Fact]
    public async Task Invoice_share_should_be_separate_from_invoice_and_book_only_after_admin_approval()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m04-7-member-obligation-{Guid.NewGuid():N}");
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

            Assert.InRange(schemaVersion, 17, int.MaxValue);
            Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync());

            var auditService = new AuditService(dbContext);
            var authenticationService = new AccountAuthenticationService(dbContext, auditService);
            var administrator = await authenticationService.InitializeFirstAdministratorAsync(
                new FirstAdministratorSetupRequest(
                    "Jan",
                    "Administrator",
                    "admin",
                    "DomioTest123"),
                Guid.NewGuid().ToString("N"));

            var userManagement = new UserManagementService(dbContext, auditService);
            var memberUserId = await userManagement.CreateUserAsync(
                new CreateUserRequest(
                    null,
                    "Anna",
                    "Domownik",
                    null,
                    null,
                    "anna-m047@example.test",
                    "DomioAnna123",
                    SystemRoles.HouseholdMemberId),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var householdService = new HouseholdFinanceService(dbContext, auditService);
            var personalService = new PersonalFinanceService(dbContext, auditService);

            var householdAccountId = await householdService.CreateAccountAsync(
                new CreateHouseholdAccountRequest(
                    "Konto Domowe",
                    HouseholdAccountTypes.Bank,
                    "PLN",
                    0m),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var privateAccountId = await personalService.CreateOwnAccountAsync(
                new CreatePersonalAccountRequest(
                    "Konto prywatne Anny",
                    PersonalAccountTypes.BankAccount,
                    "PLN",
                    500m),
                memberUserId,
                Guid.NewGuid().ToString("N"));

            var invoiceId = await householdService.CreateInvoiceAsync(
                new CreateHouseholdInvoiceRequest(
                    "Enea",
                    "FV/ENERGIA/09/2026",
                    DateTime.UtcNow.Date,
                    DateTime.UtcNow.Date.AddDays(14),
                    1000m,
                    HouseholdInvoiceCategories.Electricity),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var memberPersonId = await dbContext.UserAccounts
                .AsNoTracking()
                .Where(x => x.Id == memberUserId)
                .Select(x => x.PersonId)
                .SingleAsync();

            await householdService.AddHouseholdMemberAsync(
                new AddHouseholdMemberRequest(
                    memberPersonId),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var memberHouseholdId = await dbContext.HouseholdMembers
                .AsNoTracking()
                .Where(x =>
                    x.PersonId == memberPersonId &&
                    x.IsActive)
                .Select(x => x.Id)
                .SingleAsync();

            var obligationId = await householdService.CreateMemberObligationAsync(
                new CreateHouseholdMemberObligationRequest(
                    memberHouseholdId,
                    HouseholdMemberObligationSourceTypes.Invoice,
                    invoiceId,
                    "Udział Anny w fakturze za prąd",
                    150m,
                    DateTime.UtcNow.Date.AddDays(7),
                    householdAccountId),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var memberView = await householdService.GetMemberObligationOverviewAsync(memberUserId);
            var obligation = Assert.Single(memberView!.Obligations);
            Assert.True(obligation.IsOwn);
            Assert.Equal(150m, obligation.Amount);
            Assert.Equal(150m, obligation.OutstandingAmount);
            Assert.Equal(invoiceId, obligation.SourceInvoiceId);

            var paymentRequestId = await householdService.SubmitMemberObligationPaymentAsync(
                new SubmitHouseholdMemberObligationPaymentRequest(
                    obligationId,
                    privateAccountId,
                    100m),
                memberUserId,
                Guid.NewGuid().ToString("N"));

            var privateBalanceAfterSubmit = (await personalService.GetOwnOverviewAsync(memberUserId))
                .Accounts.Single(x => x.AccountId == privateAccountId).Balance;
            var householdBalanceAfterSubmit = (await householdService.GetOverviewAsync(administrator.UserId))!
                .Accounts.Single(x => x.AccountId == householdAccountId).Balance;

            Assert.Equal(500m, privateBalanceAfterSubmit);
            Assert.Equal(0m, householdBalanceAfterSubmit);

            var adminPendingView = await householdService.GetMemberObligationOverviewAsync(administrator.UserId);
            var adminPending = adminPendingView!.PaymentRequests.Single(x => x.PaymentRequestId == paymentRequestId);
            Assert.Null(adminPending.SourcePersonalAccountName);
            Assert.Equal(HouseholdMemberObligationPaymentStatuses.Pending, adminPending.StatusCode);

            await householdService.ApproveMemberObligationPaymentAsync(
                new ReviewHouseholdMemberObligationPaymentRequest(paymentRequestId, null),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var privateBalanceAfterApproval = (await personalService.GetOwnOverviewAsync(memberUserId))
                .Accounts.Single(x => x.AccountId == privateAccountId).Balance;
            var householdBalanceAfterApproval = (await householdService.GetOverviewAsync(administrator.UserId))!
                .Accounts.Single(x => x.AccountId == householdAccountId).Balance;

            Assert.Equal(400m, privateBalanceAfterApproval);
            Assert.Equal(100m, householdBalanceAfterApproval);

            var partialObligation = await dbContext.HouseholdMemberObligations
                .AsNoTracking()
                .SingleAsync(x => x.Id == obligationId);
            Assert.Equal(10000, partialObligation.PaidAmountMinor);
            Assert.Equal(HouseholdMemberObligationStatuses.PartiallyPaid, partialObligation.StatusCode);

            var approvedRequest = await dbContext.HouseholdMemberObligationPaymentRequests
                .AsNoTracking()
                .SingleAsync(x => x.Id == paymentRequestId);
            Assert.NotNull(approvedRequest.PersonalTransactionId);
            Assert.NotNull(approvedRequest.HouseholdEntryId);

            var personalDebit = await dbContext.PersonalFinancialTransactions
                .AsNoTracking()
                .SingleAsync(x => x.Id == approvedRequest.PersonalTransactionId);
            Assert.Equal(-10000, personalDebit.AmountMinor);
            Assert.Equal(PersonalFinanceCategories.HouseholdExtraObligation, personalDebit.CategoryCode);

            var householdCredit = await dbContext.HouseholdEntries
                .AsNoTracking()
                .SingleAsync(x => x.Id == approvedRequest.HouseholdEntryId);
            Assert.Equal(10000, householdCredit.AmountMinor);
            Assert.Equal(HouseholdEntryTypes.MemberObligationPayment, householdCredit.EntryTypeCode);
            Assert.Equal("HouseholdMemberObligationPayment", householdCredit.SourceType);

            var invoiceAfterMemberPayment = (await householdService.GetInvoiceOverviewAsync(administrator.UserId))!
                .Invoices.Single(x => x.InvoiceId == invoiceId);
            Assert.Equal(HouseholdInvoiceStatuses.Unpaid, invoiceAfterMemberPayment.StatusCode);
            Assert.Equal(0m, invoiceAfterMemberPayment.PaidAmount);
            Assert.Equal(1000m, invoiceAfterMemberPayment.RemainingAmount);
            Assert.Equal(150m, invoiceAfterMemberPayment.MemberObligationAmount);
            Assert.Equal(100m, invoiceAfterMemberPayment.MemberContributionPaidAmount);
            Assert.Equal(50m, invoiceAfterMemberPayment.MemberContributionOutstandingAmount);

            var secondRequestId = await householdService.SubmitMemberObligationPaymentAsync(
                new SubmitHouseholdMemberObligationPaymentRequest(
                    obligationId,
                    privateAccountId,
                    50m),
                memberUserId,
                Guid.NewGuid().ToString("N"));

            await householdService.ApproveMemberObligationPaymentAsync(
                new ReviewHouseholdMemberObligationPaymentRequest(secondRequestId, "Dopłata"),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var finalObligation = await dbContext.HouseholdMemberObligations
                .AsNoTracking()
                .SingleAsync(x => x.Id == obligationId);
            Assert.Equal(15000, finalObligation.PaidAmountMinor);
            Assert.Equal(HouseholdMemberObligationStatuses.Paid, finalObligation.StatusCode);

            var finalPrivateBalance = (await personalService.GetOwnOverviewAsync(memberUserId))
                .Accounts.Single(x => x.AccountId == privateAccountId).Balance;
            var finalHouseholdBalance = (await householdService.GetOverviewAsync(administrator.UserId))!
                .Accounts.Single(x => x.AccountId == householdAccountId).Balance;
            Assert.Equal(350m, finalPrivateBalance);
            Assert.Equal(150m, finalHouseholdBalance);

            var invoiceAfterFullMemberFunding =
                (await householdService.GetInvoiceOverviewAsync(administrator.UserId))!
                    .Invoices.Single(x => x.InvoiceId == invoiceId);
            Assert.Equal(150m, invoiceAfterFullMemberFunding.MemberObligationAmount);
            Assert.Equal(150m, invoiceAfterFullMemberFunding.MemberContributionPaidAmount);
            Assert.Equal(0m, invoiceAfterFullMemberFunding.MemberContributionOutstandingAmount);
            Assert.Equal(0m, invoiceAfterFullMemberFunding.PaidAmount);
            Assert.Equal(1000m, invoiceAfterFullMemberFunding.RemainingAmount);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                householdService.CancelMemberObligationAsync(
                    obligationId,
                    administrator.UserId,
                    Guid.NewGuid().ToString("N")));

            var cancellableObligationId =
                await householdService.CreateMemberObligationAsync(
                    new CreateHouseholdMemberObligationRequest(
                        memberHouseholdId,
                        HouseholdMemberObligationSourceTypes.OtherCost,
                        null,
                        "Jednorazowy dodatkowy koszt testowy",
                        25m,
                        DateTime.UtcNow.Date.AddDays(5),
                        householdAccountId),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            await householdService.CancelMemberObligationAsync(
                cancellableObligationId,
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var cancelled =
                await dbContext.HouseholdMemberObligations
                    .AsNoTracking()
                    .SingleAsync(x =>
                        x.Id ==
                            cancellableObligationId);

            Assert.Equal(
                HouseholdMemberObligationStatuses.Cancelled,
                cancelled.StatusCode);
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
