using Domio.Application.Authentication;
using Domio.Domain.HouseholdFinance;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.Notifications;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M04_10_3_InvoiceNotificationsTests
{
    [Fact]
    public async Task Invoice_notifications_should_cover_creation_deadline_partial_payment_full_payment_and_cancellation_without_duplicates()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m04-10-3-invoice-notifications-{Guid.NewGuid():N}");
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

            var invoiceId = Guid.NewGuid();
            dbContext.HouseholdInvoices.Add(
                new HouseholdInvoice
                {
                    Id = invoiceId,
                    HouseholdId = householdId,
                    Supplier = "Energia Test",
                    InvoiceNumber = "FV/10/2026",
                    IssueDateUtc = now.Date,
                    DueDateUtc = now.Date.AddDays(3),
                    GrossAmountMinor = 100000,
                    StatusCode = HouseholdInvoiceStatuses.Unpaid,
                    CategoryCode = HouseholdInvoiceCategories.Electricity,
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
                    x.EventCode == "M04.10.3.InvoiceCreated" &&
                    x.SourceId == invoiceId.ToString());

            Assert.Contains(
                first.Inbox,
                x =>
                    x.EventCode == "M04.10.3.InvoiceUpcoming" &&
                    x.SourceId == invoiceId.ToString());

            var firstPaymentId = Guid.NewGuid();
            var firstEntryId = Guid.NewGuid();

            dbContext.HouseholdEntries.Add(
                new HouseholdEntry
                {
                    Id = firstEntryId,
                    HouseholdId = householdId,
                    AccountId = householdAccountId,
                    EntryTypeCode = HouseholdEntryTypes.Expense,
                    AmountMinor = -40000,
                    OccurredAtUtc = now,
                    CategoryCode = HouseholdFinanceCategories.Utilities,
                    Description = "Płatność częściowa faktury testowej",
                    SourceType = "HouseholdInvoicePayment",
                    SourceId = firstPaymentId.ToString(),
                    CreatedByUserId = administrator.UserId,
                    CreatedAtUtc = now.AddMinutes(1)
                });

            dbContext.HouseholdInvoicePayments.Add(
                new HouseholdInvoicePayment
                {
                    Id = firstPaymentId,
                    HouseholdId = householdId,
                    InvoiceId = invoiceId,
                    HouseholdAccountId = householdAccountId,
                    CommandId = Guid.NewGuid(),
                    AmountMinor = 40000,
                    PaidAtUtc = now,
                    HouseholdEntryId = firstEntryId,
                    CreatedByUserId = administrator.UserId,
                    CreatedAtUtc = now.AddMinutes(1)
                });

            var invoice = await dbContext.HouseholdInvoices
                .SingleAsync(x => x.Id == invoiceId);
            invoice.StatusCode = HouseholdInvoiceStatuses.PartiallyPaid;
            invoice.UpdatedAtUtc = now.AddMinutes(1);

            await dbContext.SaveChangesAsync();

            var afterPartialPayment = await notificationService.GetOverviewAsync(
                administrator.UserId);

            Assert.Contains(
                afterPartialPayment.Inbox,
                x =>
                    x.EventCode == "M04.10.3.InvoicePaymentRegistered" &&
                    x.SourceId == firstPaymentId.ToString());

            Assert.Contains(
                afterPartialPayment.Inbox,
                x =>
                    x.EventCode == "M04.10.3.InvoicePartiallyPaid" &&
                    x.SourceId == invoiceId.ToString());

            var secondPaymentId = Guid.NewGuid();
            var secondEntryId = Guid.NewGuid();

            dbContext.HouseholdEntries.Add(
                new HouseholdEntry
                {
                    Id = secondEntryId,
                    HouseholdId = householdId,
                    AccountId = householdAccountId,
                    EntryTypeCode = HouseholdEntryTypes.Expense,
                    AmountMinor = -60000,
                    OccurredAtUtc = now,
                    CategoryCode = HouseholdFinanceCategories.Utilities,
                    Description = "Dopłata faktury testowej",
                    SourceType = "HouseholdInvoicePayment",
                    SourceId = secondPaymentId.ToString(),
                    CreatedByUserId = administrator.UserId,
                    CreatedAtUtc = now.AddMinutes(2)
                });

            dbContext.HouseholdInvoicePayments.Add(
                new HouseholdInvoicePayment
                {
                    Id = secondPaymentId,
                    HouseholdId = householdId,
                    InvoiceId = invoiceId,
                    HouseholdAccountId = householdAccountId,
                    CommandId = Guid.NewGuid(),
                    AmountMinor = 60000,
                    PaidAtUtc = now,
                    HouseholdEntryId = secondEntryId,
                    CreatedByUserId = administrator.UserId,
                    CreatedAtUtc = now.AddMinutes(2)
                });

            invoice.StatusCode = HouseholdInvoiceStatuses.Paid;
            invoice.UpdatedAtUtc = now.AddMinutes(2);

            var cancelledInvoiceId = Guid.NewGuid();
            dbContext.HouseholdInvoices.Add(
                new HouseholdInvoice
                {
                    Id = cancelledInvoiceId,
                    HouseholdId = householdId,
                    Supplier = "Internet Test",
                    InvoiceNumber = "NET/10/2026",
                    IssueDateUtc = now.Date,
                    DueDateUtc = now.Date.AddDays(8),
                    GrossAmountMinor = 8000,
                    StatusCode = HouseholdInvoiceStatuses.Cancelled,
                    CategoryCode = HouseholdInvoiceCategories.Internet,
                    CreatedByUserId = administrator.UserId,
                    CreatedAtUtc = now.AddMinutes(2),
                    UpdatedAtUtc = now.AddMinutes(2),
                    CancelledByUserId = administrator.UserId,
                    CancelledAtUtc = now.AddMinutes(2)
                });

            await dbContext.SaveChangesAsync();

            var afterFullPayment = await notificationService.GetOverviewAsync(
                administrator.UserId);

            Assert.Contains(
                afterFullPayment.Inbox,
                x =>
                    x.EventCode == "M04.10.3.InvoicePaymentRegistered" &&
                    x.SourceId == secondPaymentId.ToString());

            Assert.Contains(
                afterFullPayment.Inbox,
                x =>
                    x.EventCode == "M04.10.3.InvoicePaid" &&
                    x.SourceId == invoiceId.ToString());

            Assert.Contains(
                afterFullPayment.Inbox,
                x =>
                    x.EventCode == "M04.10.3.InvoiceCancelled" &&
                    x.SourceId == cancelledInvoiceId.ToString());

            var countBefore = afterFullPayment.ActivityHistory.Count;

            var repeated = await notificationService.GetOverviewAsync(
                administrator.UserId);

            Assert.Equal(countBefore, repeated.ActivityHistory.Count);
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
