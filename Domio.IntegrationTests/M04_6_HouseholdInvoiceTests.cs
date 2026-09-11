
using Domio.Application.Authentication;
using Domio.Application.HouseholdFinance;
using Domio.Domain.HouseholdFinance;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.HouseholdFinance;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M04_6_HouseholdInvoiceTests
{
    [Fact]
    public async Task Partial_payments_should_change_only_selected_invoice_and_same_command_should_be_idempotent()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"domio-m04-6-invoices-{Guid.NewGuid():N}");

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
                    .Where(x => x.Id == 1)
                    .Select(x => x.Version)
                    .SingleAsync();

            Assert.InRange(
                schemaVersion,
                15,
                int.MaxValue);

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

            var householdService =
                new HouseholdFinanceService(
                    dbContext,
                    auditService);

            var accountId =
                await householdService.CreateAccountAsync(
                    new CreateHouseholdAccountRequest(
                        "Konto domu",
                        HouseholdAccountTypes.Bank,
                        "PLN",
                        2000m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var invoiceA =
                await householdService.CreateInvoiceAsync(
                    new CreateHouseholdInvoiceRequest(
                        "Aquanet",
                        "FV/A/2026/09",
                        DateTime.UtcNow.Date,
                        DateTime.UtcNow.Date.AddDays(14),
                        1000m,
                        HouseholdInvoiceCategories.Water),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var invoiceB =
                await householdService.CreateInvoiceAsync(
                    new CreateHouseholdInvoiceRequest(
                        "Enea",
                        "FV/B/2026/09",
                        DateTime.UtcNow.Date,
                        DateTime.UtcNow.Date.AddDays(14),
                        500m,
                        HouseholdInvoiceCategories.Electricity),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var invoiceC =
                await householdService.CreateInvoiceAsync(
                    new CreateHouseholdInvoiceRequest(
                        "Internet",
                        "FV/C/2026/09",
                        DateTime.UtcNow.Date,
                        DateTime.UtcNow.Date.AddDays(14),
                        250m,
                        HouseholdInvoiceCategories.Internet),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var command1 =
                Guid.NewGuid();

            var payment1 =
                await householdService.PayInvoiceAsync(
                    new PayHouseholdInvoiceRequest(
                        command1,
                        invoiceA,
                        accountId,
                        250m,
                        DateTime.UtcNow.Date),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var retryPayment1 =
                await householdService.PayInvoiceAsync(
                    new PayHouseholdInvoiceRequest(
                        command1,
                        invoiceA,
                        accountId,
                        250m,
                        DateTime.UtcNow.Date),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            Assert.Equal(
                payment1,
                retryPayment1);

            var afterPartial =
                await householdService.GetInvoiceOverviewAsync(
                    administrator.UserId);

            Assert.NotNull(
                afterPartial);

            var aAfterPartial =
                afterPartial!.Invoices.Single(x =>
                    x.InvoiceId == invoiceA);

            var bAfterPartial =
                afterPartial.Invoices.Single(x =>
                    x.InvoiceId == invoiceB);

            var cAfterPartial =
                afterPartial.Invoices.Single(x =>
                    x.InvoiceId == invoiceC);

            Assert.Equal(
                HouseholdInvoiceStatuses.PartiallyPaid,
                aAfterPartial.StatusCode);

            Assert.Equal(
                250m,
                aAfterPartial.PaidAmount);

            Assert.Equal(
                750m,
                aAfterPartial.RemainingAmount);

            Assert.Equal(
                HouseholdInvoiceStatuses.Unpaid,
                bAfterPartial.StatusCode);

            Assert.Equal(
                500m,
                bAfterPartial.RemainingAmount);

            Assert.Equal(
                HouseholdInvoiceStatuses.Unpaid,
                cAfterPartial.StatusCode);

            Assert.Equal(
                250m,
                cAfterPartial.RemainingAmount);

            var balanceAfterPartial =
                (await householdService.GetOverviewAsync(
                    administrator.UserId))!
                    .Accounts
                    .Single(x =>
                        x.AccountId == accountId)
                    .Balance;

            Assert.Equal(
                1750m,
                balanceAfterPartial);

            Assert.Equal(
                1,
                await dbContext.HouseholdInvoicePayments
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.InvoiceId == invoiceA));

            Assert.Equal(
                1,
                await dbContext.HouseholdEntries
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.SourceType == "HouseholdInvoicePayment" &&
                        x.SourceId == payment1.ToString()));

            await householdService.PayInvoiceAsync(
                new PayHouseholdInvoiceRequest(
                    Guid.NewGuid(),
                    invoiceA,
                    accountId,
                    750m,
                    DateTime.UtcNow.Date),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var afterFull =
                await householdService.GetInvoiceOverviewAsync(
                    administrator.UserId);

            var aAfterFull =
                afterFull!.Invoices.Single(x =>
                    x.InvoiceId == invoiceA);

            Assert.Equal(
                HouseholdInvoiceStatuses.Paid,
                aAfterFull.StatusCode);

            Assert.Equal(
                0m,
                aAfterFull.RemainingAmount);

            Assert.Equal(
                HouseholdInvoiceStatuses.Unpaid,
                afterFull.Invoices.Single(x =>
                    x.InvoiceId == invoiceB).StatusCode);

            Assert.Equal(
                HouseholdInvoiceStatuses.Unpaid,
                afterFull.Invoices.Single(x =>
                    x.InvoiceId == invoiceC).StatusCode);

            var finalBalance =
                (await householdService.GetOverviewAsync(
                    administrator.UserId))!
                    .Accounts
                    .Single(x =>
                        x.AccountId == accountId)
                    .Balance;

            Assert.Equal(
                1000m,
                finalBalance);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection
                .ClearAllPools();

            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task Insufficient_balance_or_overpayment_should_not_change_invoice_or_account()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"domio-m04-6-insufficient-{Guid.NewGuid():N}");

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

            var householdService =
                new HouseholdFinanceService(
                    dbContext,
                    auditService);

            var accountId =
                await householdService.CreateAccountAsync(
                    new CreateHouseholdAccountRequest(
                        "Konto domu",
                        HouseholdAccountTypes.Bank,
                        "PLN",
                        400m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var invoiceId =
                await householdService.CreateInvoiceAsync(
                    new CreateHouseholdInvoiceRequest(
                        "Dostawca",
                        "FV/600",
                        DateTime.UtcNow.Date,
                        DateTime.UtcNow.Date.AddDays(7),
                        600m,
                        HouseholdInvoiceCategories.Other),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    householdService.PayInvoiceAsync(
                        new PayHouseholdInvoiceRequest(
                            Guid.NewGuid(),
                            invoiceId,
                            accountId,
                            600m,
                            DateTime.UtcNow.Date),
                        administrator.UserId,
                        Guid.NewGuid().ToString("N")));

            var afterInsufficient =
                await householdService.GetInvoiceOverviewAsync(
                    administrator.UserId);

            var invoice =
                afterInsufficient!.Invoices.Single(x =>
                    x.InvoiceId == invoiceId);

            Assert.Equal(
                HouseholdInvoiceStatuses.Unpaid,
                invoice.StatusCode);

            Assert.Equal(
                0m,
                invoice.PaidAmount);

            Assert.Equal(
                600m,
                invoice.RemainingAmount);

            Assert.Equal(
                400m,
                (await householdService.GetOverviewAsync(
                    administrator.UserId))!
                    .Accounts
                    .Single(x =>
                        x.AccountId == accountId)
                    .Balance);

            Assert.Empty(
                await dbContext.HouseholdInvoicePayments
                    .AsNoTracking()
                    .ToArrayAsync());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    householdService.PayInvoiceAsync(
                        new PayHouseholdInvoiceRequest(
                            Guid.NewGuid(),
                            invoiceId,
                            accountId,
                            601m,
                            DateTime.UtcNow.Date),
                        administrator.UserId,
                        Guid.NewGuid().ToString("N")));

            await householdService.CancelInvoiceAsync(
                invoiceId,
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var cancelled =
                (await householdService.GetInvoiceOverviewAsync(
                    administrator.UserId))!
                    .Invoices
                    .Single(x =>
                        x.InvoiceId == invoiceId);

            Assert.Equal(
                HouseholdInvoiceStatuses.Cancelled,
                cancelled.StatusCode);

            Assert.Equal(
                400m,
                (await householdService.GetOverviewAsync(
                    administrator.UserId))!
                    .Accounts
                    .Single(x =>
                        x.AccountId == accountId)
                    .Balance);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection
                .ClearAllPools();

            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }
}
