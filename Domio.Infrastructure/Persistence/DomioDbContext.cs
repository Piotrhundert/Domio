using Domio.Domain.Audit;
using Domio.Domain.Common;
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
                Version = 2,
                UpdatedAtUtc = new DateTime(
                    2026, 9, 8, 12, 43, 0, DateTimeKind.Utc)
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
    }
}
