using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

public partial class AddHouseholdContributionRules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "HouseholdContributionRules",
            columns: table => new
            {
                Id = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                HouseholdId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                HouseholdMemberId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                ModeCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 50,
                    nullable: false),
                FixedAmountMinor = table.Column<long>(
                    type: "INTEGER",
                    nullable: true),
                PercentageBasisPoints = table.Column<int>(
                    type: "INTEGER",
                    nullable: true),
                IncomeRuleId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: true),
                DueOffsetDays = table.Column<int>(
                    type: "INTEGER",
                    nullable: false),
                TargetHouseholdAccountId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                ValidFromUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false),
                ValidToUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: true),
                ReminderDays = table.Column<int>(
                    type: "INTEGER",
                    nullable: false),
                IsActive = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                CreatedByUserId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                CreatedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_HouseholdContributionRules",
                    x => x.Id);

                table.ForeignKey(
                    name: "FK_HouseholdContributionRules_HouseholdAccounts_TargetHouseholdAccountId",
                    column: x => x.TargetHouseholdAccountId,
                    principalTable: "HouseholdAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdContributionRules_HouseholdMembers_HouseholdMemberId",
                    column: x => x.HouseholdMemberId,
                    principalTable: "HouseholdMembers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdContributionRules_Households_HouseholdId",
                    column: x => x.HouseholdId,
                    principalTable: "Households",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdContributionRules_PersonalRecurringRules_IncomeRuleId",
                    column: x => x.IncomeRuleId,
                    principalTable: "PersonalRecurringRules",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdContributionRules_UserAccounts_CreatedByUserId",
                    column: x => x.CreatedByUserId,
                    principalTable: "UserAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "HouseholdContributionObligations",
            columns: table => new
            {
                Id = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                ContributionRuleId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                HouseholdId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                HouseholdMemberId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                TargetHouseholdAccountId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                PeriodKey = table.Column<string>(
                    type: "TEXT",
                    maxLength: 30,
                    nullable: false),
                AmountMinor = table.Column<long>(
                    type: "INTEGER",
                    nullable: false),
                PaidAmountMinor = table.Column<long>(
                    type: "INTEGER",
                    nullable: false),
                DueDateUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false),
                StatusCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 30,
                    nullable: false),
                ModeCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 50,
                    nullable: false),
                FixedAmountMinorSnapshot = table.Column<long>(
                    type: "INTEGER",
                    nullable: true),
                PercentageBasisPointsSnapshot = table.Column<int>(
                    type: "INTEGER",
                    nullable: true),
                PlannedIncomeAmountMinor = table.Column<long>(
                    type: "INTEGER",
                    nullable: true),
                IncomeRuleId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: true),
                IncomeOccurrenceId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: true),
                CreatedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_HouseholdContributionObligations",
                    x => x.Id);

                table.ForeignKey(
                    name: "FK_HouseholdContributionObligations_HouseholdAccounts_TargetHouseholdAccountId",
                    column: x => x.TargetHouseholdAccountId,
                    principalTable: "HouseholdAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdContributionObligations_HouseholdContributionRules_ContributionRuleId",
                    column: x => x.ContributionRuleId,
                    principalTable: "HouseholdContributionRules",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdContributionObligations_HouseholdMembers_HouseholdMemberId",
                    column: x => x.HouseholdMemberId,
                    principalTable: "HouseholdMembers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdContributionObligations_Households_HouseholdId",
                    column: x => x.HouseholdId,
                    principalTable: "Households",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdContributionObligations_PersonalRecurringOccurrences_IncomeOccurrenceId",
                    column: x => x.IncomeOccurrenceId,
                    principalTable: "PersonalRecurringOccurrences",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdContributionObligations_PersonalRecurringRules_IncomeRuleId",
                    column: x => x.IncomeRuleId,
                    principalTable: "PersonalRecurringRules",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionRules_CreatedByUserId",
            table: "HouseholdContributionRules",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionRules_HouseholdId",
            table: "HouseholdContributionRules",
            column: "HouseholdId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionRules_HouseholdId_IsActive",
            table: "HouseholdContributionRules",
            columns: new[] { "HouseholdId", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionRules_HouseholdMemberId",
            table: "HouseholdContributionRules",
            column: "HouseholdMemberId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionRules_IncomeRuleId",
            table: "HouseholdContributionRules",
            column: "IncomeRuleId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionRules_TargetHouseholdAccountId",
            table: "HouseholdContributionRules",
            column: "TargetHouseholdAccountId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionObligations_ContributionRuleId_PeriodKey",
            table: "HouseholdContributionObligations",
            columns: new[] { "ContributionRuleId", "PeriodKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionObligations_DueDateUtc",
            table: "HouseholdContributionObligations",
            column: "DueDateUtc");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionObligations_HouseholdId",
            table: "HouseholdContributionObligations",
            column: "HouseholdId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionObligations_HouseholdMemberId",
            table: "HouseholdContributionObligations",
            column: "HouseholdMemberId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionObligations_IncomeOccurrenceId",
            table: "HouseholdContributionObligations",
            column: "IncomeOccurrenceId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionObligations_IncomeRuleId",
            table: "HouseholdContributionObligations",
            column: "IncomeRuleId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionObligations_TargetHouseholdAccountId",
            table: "HouseholdContributionObligations",
            column: "TargetHouseholdAccountId");

        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "UpdatedAtUtc", "Version" },
            values: new object[]
            {
                new DateTime(
                    2026, 9, 10, 12, 16, 0, DateTimeKind.Utc),
                13
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "HouseholdContributionObligations");

        migrationBuilder.DropTable(
            name: "HouseholdContributionRules");

        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "UpdatedAtUtc", "Version" },
            values: new object[]
            {
                new DateTime(
                    2026, 9, 10, 11, 47, 0, DateTimeKind.Utc),
                12
            });
    }
}
