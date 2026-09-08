using Domio.Domain.Audit;
using Domio.Domain.Common;
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

    public DbSet<DatabaseMetadataRecord> DatabaseMetadata =>
        Set<DatabaseMetadataRecord>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Person> People => Set<Person>();

    public DbSet<RoleDefinition> RoleDefinitions => Set<RoleDefinition>();

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SchemaVersionRecord>(entity =>
        {
            entity.ToTable("SchemaVersions");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .ValueGeneratedNever();

            entity.Property(x => x.Version)
                .IsRequired();

            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            entity.HasData(new SchemaVersionRecord
            {
                Id = 1,
                Version = 3,
                UpdatedAtUtc = new DateTime(
                    2026, 9, 8, 15, 16, 0, DateTimeKind.Utc)
            });
        });

        modelBuilder.Entity<DatabaseMetadataRecord>(entity =>
        {
            entity.ToTable("DatabaseMetadata");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .ValueGeneratedNever();

            entity.Property(x => x.InstanceId)
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.EventType)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.EntityType)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.EntityId)
                .HasMaxLength(200);

            entity.Property(x => x.ActorId)
                .HasMaxLength(200);

            entity.Property(x => x.CorrelationId)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasMaxLength(1000);

            entity.Property(x => x.OldValuesJson);

            entity.Property(x => x.NewValuesJson);

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasIndex(x => x.CorrelationId);
            entity.HasIndex(x => x.EventType);
        });

        modelBuilder.Entity<Person>(entity =>
        {
            entity.ToTable("People");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.FirstName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.LastName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.DisplayName)
                .HasMaxLength(200);

            entity.Property(x => x.Email)
                .HasMaxLength(254);

            entity.Property(x => x.Phone)
                .HasMaxLength(50);

            entity.Property(x => x.IsActive)
                .IsRequired();

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            entity.HasIndex(x => x.LastName);
            entity.HasIndex(x => x.Email);
        });

        modelBuilder.Entity<RoleDefinition>(entity =>
        {
            entity.ToTable("RoleDefinitions");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .ValueGeneratedNever();

            entity.Property(x => x.Code)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.NamePl)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.DescriptionPl)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(x => x.IsSystem)
                .IsRequired();

            entity.HasIndex(x => x.Code)
                .IsUnique();

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

            entity.Property(x => x.LoginName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.NormalizedLoginName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.IsActive)
                .IsRequired();

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            entity.HasIndex(x => x.NormalizedLoginName)
                .IsUnique();

            entity.HasIndex(x => x.PersonId)
                .IsUnique();

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
