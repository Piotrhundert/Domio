using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DomioDbContext))]
[Migration("20260921203000_AddFamilyExpensePayments")]
public sealed class AddFamilyExpensePayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FamilyExpensePayments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                FamilyGroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                OccurrenceId = table.Column<Guid>(type: "TEXT", nullable: false),
                SourceType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                PaidFromAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                PaidFromPersonId = table.Column<Guid>(type: "TEXT", nullable: true),
                AmountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                PaidByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                PaidAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FamilyExpensePayments", x => x.Id);
                table.ForeignKey(
                    "FK_FamilyExpensePayments_FamilyGroups_FamilyGroupId",
                    x => x.FamilyGroupId,
                    "FamilyGroups",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilyExpensePayments_FamilyRecurringOccurrences_OccurrenceId",
                    x => x.OccurrenceId,
                    "FamilyRecurringOccurrences",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilyExpensePayments_People_PaidFromPersonId",
                    x => x.PaidFromPersonId,
                    "People",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilyExpensePayments_UserAccounts_PaidByUserId",
                    x => x.PaidByUserId,
                    "UserAccounts",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            "IX_FamilyExpensePayments_FamilyGroupId_PaidAtUtc",
            "FamilyExpensePayments",
            new[] { "FamilyGroupId", "PaidAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_FamilyExpensePayments_OccurrenceId",
            table: "FamilyExpensePayments",
            column: "OccurrenceId",
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_FamilyExpensePayments_PaidFromPersonId",
            "FamilyExpensePayments",
            "PaidFromPersonId");

        migrationBuilder.CreateIndex(
            "IX_FamilyExpensePayments_PaidByUserId",
            "FamilyExpensePayments",
            "PaidByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_FamilyExpensePayments_UniqueSource",
            table: "FamilyExpensePayments",
            columns: new[] { "SourceType", "SourceId" },
            unique: true);

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-21T18:30:00.0000000Z',
                "Version" = 20
            WHERE "Id" = 1;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FamilyExpensePayments");

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-21T15:00:00.0000000Z',
                "Version" = 19
            WHERE "Id" = 1;
            """);
    }
}
