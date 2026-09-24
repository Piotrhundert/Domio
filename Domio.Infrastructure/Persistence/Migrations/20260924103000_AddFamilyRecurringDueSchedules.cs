using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DomioDbContext))]
[Migration("20260924103000_AddFamilyRecurringDueSchedules")]
public sealed class AddFamilyRecurringDueSchedules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FamilyRecurringDueSchedules",
            columns: table => new
            {
                RuleId = table.Column<Guid>(type: "TEXT", nullable: false),
                ModeCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                DaysBeforeEnd = table.Column<int>(type: "INTEGER", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_FamilyRecurringDueSchedules",
                    x => x.RuleId);

                table.ForeignKey(
                    "FK_FamilyRecurringDueSchedules_FamilyRecurringRules_RuleId",
                    x => x.RuleId,
                    "FamilyRecurringRules",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-24T08:30:00.0000000Z',
                "Version" = 25
            WHERE "Id" = 1;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "FamilyRecurringDueSchedules");

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-24T07:45:00.0000000Z',
                "Version" = 24
            WHERE "Id" = 1;
            """);
    }
}
