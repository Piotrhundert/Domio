using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DomioDbContext))]
[Migration("20260921220500_AddFamilyIncomeReceipts")]
public sealed class AddFamilyIncomeReceipts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FamilyIncomeReceipts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                FamilyGroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                RuleId = table.Column<Guid>(type: "TEXT", nullable: false),
                PeriodKey = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                BeneficiaryPersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                SourceType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                ReceivedIntoAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                ReceivedIntoPersonId = table.Column<Guid>(type: "TEXT", nullable: true),
                AmountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                ReceivedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                ReceivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FamilyIncomeReceipts", x => x.Id);
                table.ForeignKey(
                    "FK_FamilyIncomeReceipts_FamilyGroups_FamilyGroupId",
                    x => x.FamilyGroupId,
                    "FamilyGroups",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilyIncomeReceipts_FamilyRecurringRules_RuleId",
                    x => x.RuleId,
                    "FamilyRecurringRules",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilyIncomeReceipts_People_BeneficiaryPersonId",
                    x => x.BeneficiaryPersonId,
                    "People",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilyIncomeReceipts_People_ReceivedIntoPersonId",
                    x => x.ReceivedIntoPersonId,
                    "People",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilyIncomeReceipts_UserAccounts_ReceivedByUserId",
                    x => x.ReceivedByUserId,
                    "UserAccounts",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            "IX_FamilyIncomeReceipts_FamilyGroupId_ReceivedAtUtc",
            "FamilyIncomeReceipts",
            new[] { "FamilyGroupId", "ReceivedAtUtc" });

        migrationBuilder.CreateIndex(
            "IX_FamilyIncomeReceipts_BeneficiaryPersonId",
            "FamilyIncomeReceipts",
            "BeneficiaryPersonId");

        migrationBuilder.CreateIndex(
            "IX_FamilyIncomeReceipts_ReceivedIntoPersonId",
            "FamilyIncomeReceipts",
            "ReceivedIntoPersonId");

        migrationBuilder.CreateIndex(
            "IX_FamilyIncomeReceipts_ReceivedByUserId",
            "FamilyIncomeReceipts",
            "ReceivedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_FamilyIncomeReceipts_RuleId_PeriodKey",
            table: "FamilyIncomeReceipts",
            columns: new[] { "RuleId", "PeriodKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FamilyIncomeReceipts_UniqueSource",
            table: "FamilyIncomeReceipts",
            columns: new[] { "SourceType", "SourceId" },
            unique: true);

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-21T20:05:00.0000000Z',
                "Version" = 21
            WHERE "Id" = 1;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FamilyIncomeReceipts");

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-21T18:30:00.0000000Z',
                "Version" = 20
            WHERE "Id" = 1;
            """);
    }
}
