using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DomioDbContext))]
[Migration("20260924094500_AddFamilyBudgetAreas")]
public sealed class AddFamilyBudgetAreas : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FamilyBudgetAreas",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                FamilyGroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                SystemCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_FamilyBudgetAreas",
                    x => x.Id);

                table.ForeignKey(
                    "FK_FamilyBudgetAreas_FamilyGroups_FamilyGroupId",
                    x => x.FamilyGroupId,
                    "FamilyGroups",
                    "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    "FK_FamilyBudgetAreas_UserAccounts_CreatedByUserId",
                    x => x.CreatedByUserId,
                    "UserAccounts",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FamilyBudgetAreas_FamilyGroupId_IsActive",
            table: "FamilyBudgetAreas",
            columns: new[]
            {
                "FamilyGroupId",
                "IsActive"
            });

        migrationBuilder.CreateIndex(
            name: "IX_FamilyBudgetAreas_FamilyGroupId_SystemCode",
            table: "FamilyBudgetAreas",
            columns: new[]
            {
                "FamilyGroupId",
                "SystemCode"
            },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FamilyBudgetAreas_CreatedByUserId",
            table: "FamilyBudgetAreas",
            column: "CreatedByUserId");

        migrationBuilder.AddColumn<Guid>(
            name: "AreaId",
            table: "FamilyRecurringRules",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_FamilyRecurringRules_AreaId",
            table: "FamilyRecurringRules",
            column: "AreaId");

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-24T07:45:00.0000000Z',
                "Version" = 24
            WHERE "Id" = 1;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_FamilyRecurringRules_AreaId",
            table: "FamilyRecurringRules");

        migrationBuilder.DropColumn(
            name: "AreaId",
            table: "FamilyRecurringRules");

        migrationBuilder.DropTable(
            name: "FamilyBudgetAreas");

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-24T07:00:00.0000000Z',
                "Version" = 23
            WHERE "Id" = 1;
            """);
    }
}
