using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

public partial class AddPersonalRecurringRules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PersonalRecurringRules",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OwnerPersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                KindCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                PlannedAmountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                FrequencyCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                CategoryCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                Counterparty = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                StartDateUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                EndDateUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PersonalRecurringRules", x => x.Id);
                table.ForeignKey(
                    name: "FK_PersonalRecurringRules_People_OwnerPersonId",
                    column: x => x.OwnerPersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PersonalRecurringRules_PersonalFinancialAccounts_AccountId",
                    column: x => x.AccountId,
                    principalTable: "PersonalFinancialAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PersonalRecurringRules_UserAccounts_CreatedByUserId",
                    column: x => x.CreatedByUserId,
                    principalTable: "UserAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PersonalRecurringOccurrences",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                RecurringRuleId = table.Column<Guid>(type: "TEXT", nullable: false),
                OwnerPersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                PeriodKey = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                PlannedDateUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                PlannedAmountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                StatusCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PersonalRecurringOccurrences", x => x.Id);
                table.ForeignKey(
                    name: "FK_PersonalRecurringOccurrences_People_OwnerPersonId",
                    column: x => x.OwnerPersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PersonalRecurringOccurrences_PersonalRecurringRules_RecurringRuleId",
                    column: x => x.RecurringRuleId,
                    principalTable: "PersonalRecurringRules",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PersonalRecurringRules_AccountId",
            table: "PersonalRecurringRules",
            column: "AccountId");
        migrationBuilder.CreateIndex(
            name: "IX_PersonalRecurringRules_CreatedByUserId",
            table: "PersonalRecurringRules",
            column: "CreatedByUserId");
        migrationBuilder.CreateIndex(
            name: "IX_PersonalRecurringRules_OwnerPersonId",
            table: "PersonalRecurringRules",
            column: "OwnerPersonId");
        migrationBuilder.CreateIndex(
            name: "IX_PersonalRecurringRules_OwnerPersonId_IsActive",
            table: "PersonalRecurringRules",
            columns: new[] { "OwnerPersonId", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_PersonalRecurringOccurrences_OwnerPersonId",
            table: "PersonalRecurringOccurrences",
            column: "OwnerPersonId");
        migrationBuilder.CreateIndex(
            name: "IX_PersonalRecurringOccurrences_PlannedDateUtc",
            table: "PersonalRecurringOccurrences",
            column: "PlannedDateUtc");
        migrationBuilder.CreateIndex(
            name: "IX_PersonalRecurringOccurrences_RecurringRuleId",
            table: "PersonalRecurringOccurrences",
            column: "RecurringRuleId");
        migrationBuilder.CreateIndex(
            name: "IX_PersonalRecurringOccurrences_RecurringRuleId_PeriodKey",
            table: "PersonalRecurringOccurrences",
            columns: new[] { "RecurringRuleId", "PeriodKey" },
            unique: true);

        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "UpdatedAtUtc", "Version" },
            values: new object[]
            {
                new DateTime(2026, 9, 10, 7, 46, 0, DateTimeKind.Utc),
                9
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PersonalRecurringOccurrences");
        migrationBuilder.DropTable(name: "PersonalRecurringRules");

        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "UpdatedAtUtc", "Version" },
            values: new object[]
            {
                new DateTime(2026, 9, 10, 6, 58, 0, DateTimeKind.Utc),
                8
            });
    }
}
