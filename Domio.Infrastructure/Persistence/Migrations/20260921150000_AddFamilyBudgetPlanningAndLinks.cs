using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DomioDbContext))]
[Migration("20260921150000_AddFamilyBudgetPlanningAndLinks")]
public sealed class AddFamilyBudgetPlanningAndLinks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FamilyRecurringRules",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                FamilyGroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                RuleTypeCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                CategoryCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                PlannedAmountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                FrequencyCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                DueDay = table.Column<int>(type: "INTEGER", nullable: false),
                BeneficiaryPersonId = table.Column<Guid>(type: "TEXT", nullable: true),
                ActiveFromUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                ActiveToUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FamilyRecurringRules", x => x.Id);
                table.ForeignKey(
                    "FK_FamilyRecurringRules_FamilyGroups_FamilyGroupId",
                    x => x.FamilyGroupId,
                    "FamilyGroups",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilyRecurringRules_People_BeneficiaryPersonId",
                    x => x.BeneficiaryPersonId,
                    "People",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilyRecurringRules_UserAccounts_CreatedByUserId",
                    x => x.CreatedByUserId,
                    "UserAccounts",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FamilyRecurringOccurrences",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                FamilyGroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                RuleId = table.Column<Guid>(type: "TEXT", nullable: false),
                PeriodKey = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                PlannedDateUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                PlannedAmountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                StatusCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                BeneficiaryPersonId = table.Column<Guid>(type: "TEXT", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FamilyRecurringOccurrences", x => x.Id);
                table.ForeignKey(
                    "FK_FamilyRecurringOccurrences_FamilyGroups_FamilyGroupId",
                    x => x.FamilyGroupId,
                    "FamilyGroups",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilyRecurringOccurrences_FamilyRecurringRules_RuleId",
                    x => x.RuleId,
                    "FamilyRecurringRules",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilyRecurringOccurrences_People_BeneficiaryPersonId",
                    x => x.BeneficiaryPersonId,
                    "People",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FamilyBudgetLinks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                FamilyGroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                PeriodKey = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true),
                SourceType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonId = table.Column<Guid>(type: "TEXT", nullable: true),
                CategoryCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                BeneficiaryPersonId = table.Column<Guid>(type: "TEXT", nullable: true),
                LinkedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                LinkedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UnlinkedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FamilyBudgetLinks", x => x.Id);
                table.ForeignKey(
                    "FK_FamilyBudgetLinks_FamilyGroups_FamilyGroupId",
                    x => x.FamilyGroupId,
                    "FamilyGroups",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilyBudgetLinks_People_PersonId",
                    x => x.PersonId,
                    "People",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilyBudgetLinks_People_BeneficiaryPersonId",
                    x => x.BeneficiaryPersonId,
                    "People",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilyBudgetLinks_UserAccounts_LinkedByUserId",
                    x => x.LinkedByUserId,
                    "UserAccounts",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            "IX_FamilyRecurringRules_FamilyGroupId_IsActive",
            "FamilyRecurringRules",
            new[] { "FamilyGroupId", "IsActive" });

        migrationBuilder.CreateIndex(
            "IX_FamilyRecurringRules_BeneficiaryPersonId",
            "FamilyRecurringRules",
            "BeneficiaryPersonId");

        migrationBuilder.CreateIndex(
            "IX_FamilyRecurringRules_CreatedByUserId",
            "FamilyRecurringRules",
            "CreatedByUserId");

        migrationBuilder.CreateIndex(
            "IX_FamilyRecurringOccurrences_FamilyGroupId_PeriodKey",
            "FamilyRecurringOccurrences",
            new[] { "FamilyGroupId", "PeriodKey" });

        migrationBuilder.CreateIndex(
            "IX_FamilyRecurringOccurrences_BeneficiaryPersonId",
            "FamilyRecurringOccurrences",
            "BeneficiaryPersonId");

        migrationBuilder.CreateIndex(
            name: "IX_FamilyRecurringOccurrences_RuleId_PeriodKey",
            table: "FamilyRecurringOccurrences",
            columns: new[] { "RuleId", "PeriodKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            "IX_FamilyBudgetLinks_FamilyGroupId_PeriodKey",
            "FamilyBudgetLinks",
            new[] { "FamilyGroupId", "PeriodKey" });

        migrationBuilder.CreateIndex(
            "IX_FamilyBudgetLinks_PersonId",
            "FamilyBudgetLinks",
            "PersonId");

        migrationBuilder.CreateIndex(
            "IX_FamilyBudgetLinks_BeneficiaryPersonId",
            "FamilyBudgetLinks",
            "BeneficiaryPersonId");

        migrationBuilder.CreateIndex(
            "IX_FamilyBudgetLinks_LinkedByUserId",
            "FamilyBudgetLinks",
            "LinkedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_FamilyBudgetLinks_UniqueSource",
            table: "FamilyBudgetLinks",
            columns: new[] { "FamilyGroupId", "SourceType", "SourceId" },
            unique: true);

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-21T15:00:00.0000000Z',
                "Version" = 19
            WHERE "Id" = 1;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FamilyBudgetLinks");
        migrationBuilder.DropTable(name: "FamilyRecurringOccurrences");
        migrationBuilder.DropTable(name: "FamilyRecurringRules");

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-21T12:40:00.0000000Z',
                "Version" = 18
            WHERE "Id" = 1;
            """);
    }
}
