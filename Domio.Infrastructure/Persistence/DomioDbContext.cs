using Domio.Domain.Audit;
using Domio.Domain.Common;
using Domio.Domain.HouseholdFinance;
using Domio.Domain.PersonalFinance;
using Domio.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Domio.Infrastructure.Persistence;

public sealed class DomioDbContext : DbContext
{
    public DomioDbContext(DbContextOptions<DomioDbContext> options)
        : base(options)
    {
    }

    public DbSet<SchemaVersionRecord> SchemaVersions => Set<SchemaVersionRecord>();
    public DbSet<DatabaseMetadataRecord> DatabaseMetadata => Set<DatabaseMetadataRecord>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<HouseholdAccount> HouseholdAccounts => Set<HouseholdAccount>();
    public DbSet<HouseholdEntry> HouseholdEntries => Set<HouseholdEntry>();
    public DbSet<HouseholdContributionRule> HouseholdContributionRules =>
        Set<HouseholdContributionRule>();
    public DbSet<HouseholdContributionObligation> HouseholdContributionObligations =>
        Set<HouseholdContributionObligation>();
    public DbSet<HouseholdContributionPaymentRequest> HouseholdContributionPaymentRequests =>
        Set<HouseholdContributionPaymentRequest>();
    public DbSet<HouseholdInvoice> HouseholdInvoices =>
        Set<HouseholdInvoice>();
    public DbSet<HouseholdInvoicePayment> HouseholdInvoicePayments =>
        Set<HouseholdInvoicePayment>();
    public DbSet<PersonalFinancialAccount> PersonalFinancialAccounts =>
        Set<PersonalFinancialAccount>();
    public DbSet<PersonalFinancialTransaction> PersonalFinancialTransactions =>
        Set<PersonalFinancialTransaction>();
    public DbSet<PersonalRecurringRule> PersonalRecurringRules =>
        Set<PersonalRecurringRule>();
    public DbSet<PersonalRecurringOccurrence> PersonalRecurringOccurrences =>
        Set<PersonalRecurringOccurrence>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<PersonProfile> PersonProfiles => Set<PersonProfile>();
    public DbSet<RoleDefinition> RoleDefinitions => Set<RoleDefinition>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SchemaVersionRecord>(entity =>
        {
            entity.ToTable("SchemaVersions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Version).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();

            entity.HasData(new SchemaVersionRecord
            {
                Id = 1,
                Version = 16,
                UpdatedAtUtc = new DateTime(
                    2026, 9, 11, 12, 50, 0, DateTimeKind.Utc)
            });
        });

        modelBuilder.Entity<DatabaseMetadataRecord>(entity =>
        {
            entity.ToTable("DatabaseMetadata");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.InstanceId).HasMaxLength(32).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(200);
            entity.Property(x => x.ActorId).HasMaxLength(200);
            entity.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.OldValuesJson);
            entity.Property(x => x.NewValuesJson);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasIndex(x => x.CorrelationId);
            entity.HasIndex(x => x.EventType);
        });

        modelBuilder.Entity<Household>(entity =>
        {
            entity.ToTable("Households");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name)
                .HasMaxLength(160)
                .IsRequired();
            entity.Property(x => x.CurrencyCode)
                .HasMaxLength(3)
                .IsRequired();
            entity.Property(x => x.IsActive)
                .IsRequired();
            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();
            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            entity.HasIndex(x => x.IsActive);

            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HouseholdMember>(entity =>
        {
            entity.ToTable("HouseholdMembers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IsActive)
                .IsRequired();
            entity.Property(x => x.JoinedAtUtc)
                .IsRequired();
            entity.Property(x => x.LeftAtUtc);

            entity.HasIndex(x => x.HouseholdId);
            entity.HasIndex(x => x.PersonId);
            entity.HasIndex(x => new
            {
                x.HouseholdId,
                x.PersonId
            })
                .IsUnique();

            entity.HasOne<Household>()
                .WithMany()
                .HasForeignKey(x => x.HouseholdId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Person>()
                .WithMany()
                .HasForeignKey(x => x.PersonId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HouseholdAccount>(entity =>
        {
            entity.ToTable("HouseholdAccounts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name)
                .HasMaxLength(120)
                .IsRequired();
            entity.Property(x => x.AccountTypeCode)
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(x => x.CurrencyCode)
                .HasMaxLength(3)
                .IsRequired();
            entity.Property(x => x.IsActive)
                .IsRequired();
            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();
            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired();
            entity.Property(x => x.ArchivedAtUtc);

            entity.HasIndex(x => x.HouseholdId);
            entity.HasIndex(x => new
            {
                x.HouseholdId,
                x.IsActive
            });

            entity.HasOne<Household>()
                .WithMany()
                .HasForeignKey(x => x.HouseholdId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HouseholdEntry>(entity =>
        {
            entity.ToTable("HouseholdEntries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EntryTypeCode)
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(x => x.AmountMinor)
                .IsRequired();
            entity.Property(x => x.OccurredAtUtc)
                .IsRequired();
            entity.Property(x => x.CategoryCode)
                .HasMaxLength(50);
            entity.Property(x => x.Description)
                .HasMaxLength(500);
            entity.Property(x => x.SourceType)
                .HasMaxLength(100);
            entity.Property(x => x.SourceId)
                .HasMaxLength(200);
            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.HasIndex(x => x.HouseholdId);
            entity.HasIndex(x => x.AccountId);
            entity.HasIndex(x => x.CategoryCode);
            entity.HasIndex(x => new
            {
                x.HouseholdId,
                x.OccurredAtUtc
            });
            entity.HasIndex(x => new
            {
                x.SourceType,
                x.SourceId
            });
            entity.HasIndex(x => x.CorrectsEntryId);

            entity.HasOne<Household>()
                .WithMany()
                .HasForeignKey(x => x.HouseholdId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<HouseholdAccount>()
                .WithMany()
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<HouseholdEntry>()
                .WithMany()
                .HasForeignKey(x => x.CorrectsEntryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HouseholdContributionRule>(entity =>
        {
            entity.ToTable("HouseholdContributionRules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ModeCode)
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(x => x.FixedAmountMinor);
            entity.Property(x => x.PercentageBasisPoints);
            entity.Property(x => x.IncomeRuleId);
            entity.Property(x => x.DueOffsetDays)
                .IsRequired();
            entity.Property(x => x.ValidFromUtc)
                .IsRequired();
            entity.Property(x => x.ValidToUtc);
            entity.Property(x => x.ReminderDays)
                .IsRequired();
            entity.Property(x => x.IsActive)
                .IsRequired();
            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();
            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            entity.HasIndex(x => x.HouseholdId);
            entity.HasIndex(x => x.HouseholdMemberId);
            entity.HasIndex(x => x.TargetHouseholdAccountId);
            entity.HasIndex(x => x.IncomeRuleId);
            entity.HasIndex(x => new
            {
                x.HouseholdId,
                x.IsActive
            });

            entity.HasOne<Household>()
                .WithMany()
                .HasForeignKey(x => x.HouseholdId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<HouseholdMember>()
                .WithMany()
                .HasForeignKey(x => x.HouseholdMemberId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<HouseholdAccount>()
                .WithMany()
                .HasForeignKey(x => x.TargetHouseholdAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<PersonalRecurringRule>()
                .WithMany()
                .HasForeignKey(x => x.IncomeRuleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HouseholdContributionObligation>(entity =>
        {
            entity.ToTable("HouseholdContributionObligations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PeriodKey)
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(x => x.AmountMinor)
                .IsRequired();
            entity.Property(x => x.PaidAmountMinor)
                .IsRequired();
            entity.Property(x => x.DueDateUtc)
                .IsRequired();
            entity.Property(x => x.StatusCode)
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(x => x.ModeCode)
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(x => x.FixedAmountMinorSnapshot);
            entity.Property(x => x.PercentageBasisPointsSnapshot);
            entity.Property(x => x.PlannedIncomeAmountMinor);
            entity.Property(x => x.IncomeRuleId);
            entity.Property(x => x.IncomeOccurrenceId);
            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();
            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            entity.HasIndex(x => x.HouseholdId);
            entity.HasIndex(x => x.HouseholdMemberId);
            entity.HasIndex(x => x.TargetHouseholdAccountId);
            entity.HasIndex(x => x.IncomeRuleId);
            entity.HasIndex(x => x.IncomeOccurrenceId);
            entity.HasIndex(x => x.DueDateUtc);
            entity.HasIndex(x => new
            {
                x.ContributionRuleId,
                x.PeriodKey
            })
                .IsUnique();

            entity.HasOne<HouseholdContributionRule>()
                .WithMany()
                .HasForeignKey(x => x.ContributionRuleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Household>()
                .WithMany()
                .HasForeignKey(x => x.HouseholdId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<HouseholdMember>()
                .WithMany()
                .HasForeignKey(x => x.HouseholdMemberId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<HouseholdAccount>()
                .WithMany()
                .HasForeignKey(x => x.TargetHouseholdAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<PersonalRecurringRule>()
                .WithMany()
                .HasForeignKey(x => x.IncomeRuleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<PersonalRecurringOccurrence>()
                .WithMany()
                .HasForeignKey(x => x.IncomeOccurrenceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HouseholdContributionPaymentRequest>(entity =>
        {
            entity.ToTable("HouseholdContributionPaymentRequests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AmountMinor)
                .IsRequired();
            entity.Property(x => x.StatusCode)
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(x => x.SubmittedAtUtc)
                .IsRequired();
            entity.Property(x => x.ReviewedAtUtc);
            entity.Property(x => x.ReviewNote)
                .HasMaxLength(500);
            entity.Property(x => x.PersonalTransactionId);
            entity.Property(x => x.HouseholdEntryId);

            entity.HasIndex(x => x.HouseholdId);
            entity.HasIndex(x => x.HouseholdMemberId);
            entity.HasIndex(x => x.ObligationId);
            entity.HasIndex(x => x.SourcePersonalAccountId);
            entity.HasIndex(x => x.TargetHouseholdAccountId);
            entity.HasIndex(x => x.StatusCode);
            entity.HasIndex(x => x.SubmittedAtUtc);
            entity.HasIndex(x => x.SubmittedByUserId);
            entity.HasIndex(x => x.ReviewedByUserId);
            entity.HasIndex(x => x.PersonalTransactionId)
                .IsUnique();
            entity.HasIndex(x => x.HouseholdEntryId)
                .IsUnique();

            entity.HasOne<Household>()
                .WithMany()
                .HasForeignKey(x => x.HouseholdId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<HouseholdMember>()
                .WithMany()
                .HasForeignKey(x => x.HouseholdMemberId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<HouseholdContributionObligation>()
                .WithMany()
                .HasForeignKey(x => x.ObligationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<PersonalFinancialAccount>()
                .WithMany()
                .HasForeignKey(x => x.SourcePersonalAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<HouseholdAccount>()
                .WithMany()
                .HasForeignKey(x => x.TargetHouseholdAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(x => x.SubmittedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(x => x.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<PersonalFinancialTransaction>()
                .WithMany()
                .HasForeignKey(x => x.PersonalTransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<HouseholdEntry>()
                .WithMany()
                .HasForeignKey(x => x.HouseholdEntryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HouseholdInvoice>(entity =>
        {
            entity.ToTable("HouseholdInvoices");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Supplier)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(x => x.InvoiceNumber)
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(x => x.IssueDateUtc)
                .IsRequired();
            entity.Property(x => x.DueDateUtc)
                .IsRequired();
            entity.Property(x => x.GrossAmountMinor)
                .IsRequired();
            entity.Property(x => x.StatusCode)
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(x => x.CategoryCode)
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(x => x.BillingPeriodFromUtc);
            entity.Property(x => x.BillingPeriodToUtc);
            entity.Property(x => x.MainMeterNumber)
                .HasMaxLength(100);
            entity.Property(x => x.MainMeterUnit)
                .HasMaxLength(20);
            entity.Property(x => x.MainMeterPreviousReading)
                .HasMaxLength(40);
            entity.Property(x => x.MainMeterCurrentReading)
                .HasMaxLength(40);
            entity.Property(x => x.SubmeterReadingsSnapshot)
                .HasMaxLength(4000);
            entity.Property(x => x.UtilityInvoiceId);
            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();
            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired();
            entity.Property(x => x.CancelledAtUtc);

            entity.HasIndex(x => x.HouseholdId);
            entity.HasIndex(x => x.DueDateUtc);
            entity.HasIndex(x => x.StatusCode);
            entity.HasIndex(x => x.CategoryCode);
            entity.HasIndex(x => x.UtilityInvoiceId);
            entity.HasIndex(x => x.CreatedByUserId);
            entity.HasIndex(x => x.CancelledByUserId);
            entity.HasIndex(x => new
            {
                x.HouseholdId,
                x.Supplier,
                x.InvoiceNumber
            })
                .IsUnique();

            entity.HasOne<Household>()
                .WithMany()
                .HasForeignKey(x => x.HouseholdId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(x => x.CancelledByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HouseholdInvoicePayment>(entity =>
        {
            entity.ToTable("HouseholdInvoicePayments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CommandId)
                .IsRequired();
            entity.Property(x => x.AmountMinor)
                .IsRequired();
            entity.Property(x => x.PaidAtUtc)
                .IsRequired();
            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.HasIndex(x => x.HouseholdId);
            entity.HasIndex(x => x.InvoiceId);
            entity.HasIndex(x => x.HouseholdAccountId);
            entity.HasIndex(x => x.PaidAtUtc);
            entity.HasIndex(x => x.HouseholdEntryId)
                .IsUnique();
            entity.HasIndex(x => x.CreatedByUserId);
            entity.HasIndex(x => new
            {
                x.HouseholdId,
                x.CommandId
            })
                .IsUnique();

            entity.HasOne<Household>()
                .WithMany()
                .HasForeignKey(x => x.HouseholdId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<HouseholdInvoice>()
                .WithMany()
                .HasForeignKey(x => x.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<HouseholdAccount>()
                .WithMany()
                .HasForeignKey(x => x.HouseholdAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<HouseholdEntry>()
                .WithMany()
                .HasForeignKey(x => x.HouseholdEntryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PersonalFinancialAccount>(entity =>
        {
            entity.ToTable("PersonalFinancialAccounts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name)
                .HasMaxLength(120)
                .IsRequired();
            entity.Property(x => x.AccountTypeCode)
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(x => x.CurrencyCode)
                .HasMaxLength(3)
                .IsRequired();
            entity.Property(x => x.IsActive)
                .IsRequired();
            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();
            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired();
            entity.Property(x => x.ArchivedAtUtc);

            entity.HasIndex(x => x.OwnerPersonId);
            entity.HasIndex(x => new
            {
                x.OwnerPersonId,
                x.IsActive
            });

            entity.HasOne<Person>()
                .WithMany()
                .HasForeignKey(x => x.OwnerPersonId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PersonalFinancialTransaction>(entity =>
        {
            entity.ToTable("PersonalFinancialTransactions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.KindCode)
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(x => x.AmountMinor)
                .IsRequired();
            entity.Property(x => x.OccurredAtUtc)
                .IsRequired();
            entity.Property(x => x.CategoryCode)
                .HasMaxLength(50);
            entity.Property(x => x.Counterparty)
                .HasMaxLength(200);
            entity.Property(x => x.Description)
                .HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.HasIndex(x => x.AccountId);
            entity.HasIndex(x => x.OwnerPersonId);
            entity.HasIndex(x => x.CategoryCode);
            entity.HasIndex(x => new
            {
                x.OwnerPersonId,
                x.OccurredAtUtc
            });
            entity.HasIndex(x => x.CorrectsTransactionId);

            entity.HasOne<PersonalFinancialAccount>()
                .WithMany()
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Person>()
                .WithMany()
                .HasForeignKey(x => x.OwnerPersonId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<PersonalFinancialTransaction>()
                .WithMany()
                .HasForeignKey(x => x.CorrectsTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PersonalRecurringRule>(entity =>
        {
            entity.ToTable("PersonalRecurringRules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.KindCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.PlannedAmountMinor).IsRequired();
            entity.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(x => x.FrequencyCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.CategoryCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Counterparty).HasMaxLength(200);
            entity.Property(x => x.StartDateUtc).IsRequired();
            entity.Property(x => x.EndDateUtc);
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();

            entity.HasIndex(x => x.OwnerPersonId);
            entity.HasIndex(x => x.AccountId);
            entity.HasIndex(x => new { x.OwnerPersonId, x.IsActive });

            entity.HasOne<Person>()
                .WithMany()
                .HasForeignKey(x => x.OwnerPersonId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<PersonalFinancialAccount>()
                .WithMany()
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PersonalRecurringOccurrence>(entity =>
        {
            entity.ToTable("PersonalRecurringOccurrences");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PeriodKey).HasMaxLength(30).IsRequired();
            entity.Property(x => x.PlannedDateUtc).IsRequired();
            entity.Property(x => x.PlannedAmountMinor).IsRequired();
            entity.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(x => x.AccountId);
            entity.Property(x => x.AccountName).HasMaxLength(120);
            entity.Property(x => x.KindCode).HasMaxLength(50);
            entity.Property(x => x.RuleName).HasMaxLength(160);
            entity.Property(x => x.CategoryCode).HasMaxLength(50);
            entity.Property(x => x.Counterparty).HasMaxLength(200);
            entity.Property(x => x.StatusCode).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ActualTransactionId);
            entity.Property(x => x.ActualAmountMinor);
            entity.Property(x => x.ActualDateUtc);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();

            entity.HasIndex(x => x.OwnerPersonId);
            entity.HasIndex(x => x.RecurringRuleId);
            entity.HasIndex(x => x.AccountId);
            entity.HasIndex(x => x.PlannedDateUtc);
            entity.HasIndex(x => x.ActualTransactionId);
            entity.HasIndex(x => new { x.RecurringRuleId, x.PeriodKey })
                .IsUnique();

            entity.HasOne<PersonalRecurringRule>()
                .WithMany()
                .HasForeignKey(x => x.RecurringRuleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Person>()
                .WithMany()
                .HasForeignKey(x => x.OwnerPersonId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<PersonalFinancialAccount>()
                .WithMany()
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<PersonalFinancialTransaction>()
                .WithMany()
                .HasForeignKey(x => x.ActualTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Person>(entity =>
        {
            entity.ToTable("People");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(200);
            entity.Property(x => x.Email).HasMaxLength(254);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.PersonTypeCode).HasMaxLength(50);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.Property(x => x.ArchivedAtUtc);
            entity.HasIndex(x => x.LastName);
            entity.HasIndex(x => x.Email);
            entity.HasIndex(x => x.PersonTypeCode);
        });

        modelBuilder.Entity<PersonProfile>(entity =>
        {
            entity.ToTable("PersonProfiles");
            entity.HasKey(x => x.PersonId);

            entity.Property(x => x.BirthDate);
            entity.Property(x => x.Pesel).HasMaxLength(11);
            entity.Property(x => x.Nationality).HasMaxLength(100);

            entity.Property(x => x.IdentityDocumentTypeCode)
                .HasMaxLength(50);
            entity.Property(x => x.IdentityDocumentNumber)
                .HasMaxLength(100);
            entity.Property(x => x.IdentityDocumentIssuingCountry)
                .HasMaxLength(100);
            entity.Property(x => x.IdentityDocumentIssuedOn);
            entity.Property(x => x.IdentityDocumentExpiresOn);

            entity.Property(x => x.PreferredContactMethodCode)
                .HasMaxLength(50);

            entity.Property(x => x.CorrespondenceCountry)
                .HasMaxLength(100);
            entity.Property(x => x.CorrespondenceRegion)
                .HasMaxLength(100);
            entity.Property(x => x.CorrespondenceCity)
                .HasMaxLength(100);
            entity.Property(x => x.CorrespondencePostalCode)
                .HasMaxLength(20);
            entity.Property(x => x.CorrespondenceStreet)
                .HasMaxLength(150);
            entity.Property(x => x.CorrespondenceBuildingNumber)
                .HasMaxLength(30);
            entity.Property(x => x.CorrespondenceUnitNumber)
                .HasMaxLength(30);
            entity.Property(x => x.CorrespondenceNotes)
                .HasMaxLength(500);

            entity.Property(x => x.EmergencyContactFirstName)
                .HasMaxLength(100);
            entity.Property(x => x.EmergencyContactLastName)
                .HasMaxLength(100);
            entity.Property(x => x.EmergencyContactRelation)
                .HasMaxLength(100);
            entity.Property(x => x.EmergencyContactPhone)
                .HasMaxLength(50);
            entity.Property(x => x.EmergencyContactEmail)
                .HasMaxLength(254);
            entity.Property(x => x.EmergencyContactNotes)
                .HasMaxLength(500);

            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();

            entity.HasIndex(x => x.Pesel)
                .IsUnique();

            entity.HasOne<Person>()
                .WithOne()
                .HasForeignKey<PersonProfile>(x => x.PersonId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoleDefinition>(entity =>
        {
            entity.ToTable("RoleDefinitions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.NamePl).HasMaxLength(100).IsRequired();
            entity.Property(x => x.DescriptionPl).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.IsSystem).IsRequired();
            entity.Property(x => x.PermissionConfigurationJson)
                .HasMaxLength(16000);
            entity.HasIndex(x => x.Code).IsUnique();

            entity.HasData(
                new RoleDefinition
                {
                    Id = SystemRoles.AdministratorId,
                    Code = SystemRoles.AdministratorCode,
                    NamePl = "Administrator",
                    DescriptionPl =
                        "Pełne zarządzanie Domio: użytkownicy, konfiguracja, finanse gospodarstwa, umowy, media i rozliczenia.",
                    IsSystem = true
                },
                new RoleDefinition
                {
                    Id = SystemRoles.HouseholdMemberId,
                    Code = SystemRoles.HouseholdMemberCode,
                    NamePl = "Domownik",
                    DescriptionPl =
                        "Członek gospodarstwa domowego. Korzysta z funkcji domowych oraz własnych finansów zgodnie z nadanymi uprawnieniami.",
                    IsSystem = true
                },
                new RoleDefinition
                {
                    Id = SystemRoles.TenantId,
                    Code = SystemRoles.TenantCode,
                    NamePl = "Lokator",
                    DescriptionPl =
                        "Osoba objęta najmem. Dostęp do własnej umowy, rozliczeń, należności i płatności udostępnionych przez system.",
                    IsSystem = true
                },
                new RoleDefinition
                {
                    Id = SystemRoles.GuestId,
                    Code = SystemRoles.GuestCode,
                    NamePl = "Gość",
                    DescriptionPl =
                        "Ograniczony dostęp do wybranych informacji i funkcji bez uprawnień administracyjnych.",
                    IsSystem = true
                },
                new RoleDefinition
                {
                    Id = SystemRoles.ChildId,
                    Code = SystemRoles.ChildCode,
                    NamePl = "Dziecko",
                    DescriptionPl =
                        "Ograniczony profil członka gospodarstwa przeznaczony dla dziecka; zakres dostępu kontroluje administrator.",
                    IsSystem = true
                });
        });

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("UserAccounts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LoginName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.NormalizedLoginName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(254);
            entity.Property(x => x.NormalizedEmail).HasMaxLength(254);
            entity.Property(x => x.PasswordHash).HasMaxLength(512);
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.FailedLoginAttempts).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.HasIndex(x => x.NormalizedLoginName).IsUnique();
            entity.HasIndex(x => x.NormalizedEmail).IsUnique();
            entity.HasIndex(x => x.PersonId).IsUnique();
            entity.HasIndex(x => x.RoleDefinitionId);

            entity.HasOne<Person>()
                .WithOne()
                .HasForeignKey<UserAccount>(x => x.PersonId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<RoleDefinition>()
                .WithMany()
                .HasForeignKey(x => x.RoleDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
