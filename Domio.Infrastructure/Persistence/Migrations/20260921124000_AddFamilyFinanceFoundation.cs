using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DomioDbContext))]
[Migration("20260921124000_AddFamilyFinanceFoundation")]
public sealed class AddFamilyFinanceFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FamilyGroups",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FamilyGroups", x => x.Id);
                table.ForeignKey(
                    "FK_FamilyGroups_Households_HouseholdId",
                    x => x.HouseholdId,
                    "Households",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilyGroups_UserAccounts_CreatedByUserId",
                    x => x.CreatedByUserId,
                    "UserAccounts",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FamilyMembers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                FamilyGroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                FamilyRoleCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                ValidFromUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                ValidToUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FamilyMembers", x => x.Id);
                table.ForeignKey(
                    "FK_FamilyMembers_FamilyGroups_FamilyGroupId",
                    x => x.FamilyGroupId,
                    "FamilyGroups",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilyMembers_People_PersonId",
                    x => x.PersonId,
                    "People",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilyMembers_UserAccounts_CreatedByUserId",
                    x => x.CreatedByUserId,
                    "UserAccounts",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FamilySharingPolicies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                FamilyGroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                SharePlannedIncome = table.Column<bool>(type: "INTEGER", nullable: false),
                ShareActualIncome = table.Column<bool>(type: "INTEGER", nullable: false),
                ShareFamilyExpenses = table.Column<bool>(type: "INTEGER", nullable: false),
                ShareRecurringRules = table.Column<bool>(type: "INTEGER", nullable: false),
                EffectiveFromUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                EffectiveToUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FamilySharingPolicies", x => x.Id);
                table.ForeignKey(
                    "FK_FamilySharingPolicies_FamilyGroups_FamilyGroupId",
                    x => x.FamilyGroupId,
                    "FamilyGroups",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilySharingPolicies_People_PersonId",
                    x => x.PersonId,
                    "People",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilySharingPolicies_UserAccounts_CreatedByUserId",
                    x => x.CreatedByUserId,
                    "UserAccounts",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            "IX_FamilyGroups_HouseholdId",
            "FamilyGroups",
            "HouseholdId");

        migrationBuilder.CreateIndex(
            "IX_FamilyGroups_HouseholdId_IsActive",
            "FamilyGroups",
            new[] { "HouseholdId", "IsActive" });

        migrationBuilder.CreateIndex(
            "IX_FamilyGroups_CreatedByUserId",
            "FamilyGroups",
            "CreatedByUserId");

        migrationBuilder.CreateIndex(
            "IX_FamilyMembers_FamilyGroupId",
            "FamilyMembers",
            "FamilyGroupId");

        migrationBuilder.CreateIndex(
            "IX_FamilyMembers_PersonId",
            "FamilyMembers",
            "PersonId");

        migrationBuilder.CreateIndex(
            "IX_FamilyMembers_CreatedByUserId",
            "FamilyMembers",
            "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_FamilyMembers_ActiveMembership",
            table: "FamilyMembers",
            columns: new[] { "FamilyGroupId", "PersonId" },
            unique: true,
            filter: "ValidToUtc IS NULL");

        migrationBuilder.CreateIndex(
            "IX_FamilySharingPolicies_FamilyGroupId",
            "FamilySharingPolicies",
            "FamilyGroupId");

        migrationBuilder.CreateIndex(
            "IX_FamilySharingPolicies_PersonId",
            "FamilySharingPolicies",
            "PersonId");

        migrationBuilder.CreateIndex(
            "IX_FamilySharingPolicies_CreatedByUserId",
            "FamilySharingPolicies",
            "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_FamilySharingPolicies_ActivePolicy",
            table: "FamilySharingPolicies",
            columns: new[] { "FamilyGroupId", "PersonId" },
            unique: true,
            filter: "EffectiveToUtc IS NULL");

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-21T12:40:00.0000000Z',
                "Version" = 18
            WHERE "Id" = 1;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "FamilySharingPolicies");

        migrationBuilder.DropTable(
            name: "FamilyMembers");

        migrationBuilder.DropTable(
            name: "FamilyGroups");

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-11T16:40:00.0000000Z',
                "Version" = 17
            WHERE "Id" = 1;
            """);
    }
}
