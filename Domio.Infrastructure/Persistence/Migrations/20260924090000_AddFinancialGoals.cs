using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DomioDbContext))]
[Migration("20260924090000_AddFinancialGoals")]
public sealed class AddFinancialGoals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FinancialGoals",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ScopeCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                OwnerPersonId = table.Column<Guid>(type: "TEXT", nullable: true),
                HouseholdId = table.Column<Guid>(type: "TEXT", nullable: true),
                FamilyGroupId = table.Column<Guid>(type: "TEXT", nullable: true),
                Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                CategoryCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                TargetAmountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                TargetDateUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                ArchivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FinancialGoals", x => x.Id);
                table.ForeignKey(
                    "FK_FinancialGoals_People_OwnerPersonId",
                    x => x.OwnerPersonId,
                    "People",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FinancialGoals_Households_HouseholdId",
                    x => x.HouseholdId,
                    "Households",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FinancialGoals_FamilyGroups_FamilyGroupId",
                    x => x.FamilyGroupId,
                    "FamilyGroups",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FinancialGoals_UserAccounts_CreatedByUserId",
                    x => x.CreatedByUserId,
                    "UserAccounts",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FinancialGoalContributions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                GoalId = table.Column<Guid>(type: "TEXT", nullable: false),
                AmountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                ContributedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FinancialGoalContributions", x => x.Id);
                table.ForeignKey(
                    "FK_FinancialGoalContributions_FinancialGoals_GoalId",
                    x => x.GoalId,
                    "FinancialGoals",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FinancialGoalContributions_UserAccounts_CreatedByUserId",
                    x => x.CreatedByUserId,
                    "UserAccounts",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            "IX_FinancialGoals_ScopeCode_OwnerPersonId",
            "FinancialGoals",
            new[] { "ScopeCode", "OwnerPersonId" });

        migrationBuilder.CreateIndex(
            "IX_FinancialGoals_ScopeCode_HouseholdId",
            "FinancialGoals",
            new[] { "ScopeCode", "HouseholdId" });

        migrationBuilder.CreateIndex(
            "IX_FinancialGoals_ScopeCode_FamilyGroupId",
            "FinancialGoals",
            new[] { "ScopeCode", "FamilyGroupId" });

        migrationBuilder.CreateIndex(
            "IX_FinancialGoals_IsActive",
            "FinancialGoals",
            "IsActive");

        migrationBuilder.CreateIndex(
            "IX_FinancialGoalContributions_GoalId",
            "FinancialGoalContributions",
            "GoalId");

        migrationBuilder.CreateIndex(
            "IX_FinancialGoalContributions_ContributedAtUtc",
            "FinancialGoalContributions",
            "ContributedAtUtc");

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-24T07:00:00.0000000Z',
                "Version" = 23
            WHERE "Id" = 1;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FinancialGoalContributions");
        migrationBuilder.DropTable(name: "FinancialGoals");

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-23T10:30:00.0000000Z',
                "Version" = 22
            WHERE "Id" = 1;
            """);
    }
}
