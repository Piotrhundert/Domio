using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

public partial class AddAuditAndDiagnostics : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AuditLogs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                EventType = table.Column<string>(
                    type: "TEXT",
                    maxLength: 100,
                    nullable: false),
                EntityType = table.Column<string>(
                    type: "TEXT",
                    maxLength: 100,
                    nullable: false),
                EntityId = table.Column<string>(
                    type: "TEXT",
                    maxLength: 200,
                    nullable: true),
                ActorId = table.Column<string>(
                    type: "TEXT",
                    maxLength: 200,
                    nullable: true),
                CorrelationId = table.Column<string>(
                    type: "TEXT",
                    maxLength: 64,
                    nullable: false),
                Description = table.Column<string>(
                    type: "TEXT",
                    maxLength: 1000,
                    nullable: true),
                OldValuesJson = table.Column<string>(
                    type: "TEXT",
                    nullable: true),
                NewValuesJson = table.Column<string>(
                    type: "TEXT",
                    nullable: true),
                CreatedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditLogs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "DatabaseMetadata",
            columns: table => new
            {
                Id = table.Column<int>(
                    type: "INTEGER",
                    nullable: false),
                InstanceId = table.Column<string>(
                    type: "TEXT",
                    maxLength: 32,
                    nullable: false),
                CreatedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DatabaseMetadata", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_CorrelationId",
            table: "AuditLogs",
            column: "CorrelationId");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_CreatedAtUtc",
            table: "AuditLogs",
            column: "CreatedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_EventType",
            table: "AuditLogs",
            column: "EventType");

        migrationBuilder.Sql(
            """
            INSERT INTO "DatabaseMetadata" ("Id", "InstanceId", "CreatedAtUtc")
            VALUES (1, lower(hex(randomblob(16))), CURRENT_TIMESTAMP);
            """);

        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "UpdatedAtUtc", "Version" },
            values: new object[]
            {
                new DateTime(2026, 9, 8, 12, 43, 0, DateTimeKind.Utc),
                2
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "UpdatedAtUtc", "Version" },
            values: new object[]
            {
                new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc),
                1
            });

        migrationBuilder.DropTable(
            name: "AuditLogs");

        migrationBuilder.DropTable(
            name: "DatabaseMetadata");
    }
}
