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
                Version = 1,
                UpdatedAtUtc = new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc)
            });
        });
    }
}
