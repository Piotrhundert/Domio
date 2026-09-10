using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

public partial class AddRecurringOccurrenceConfirmation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "ActualAmountMinor",
            table: "PersonalRecurringOccurrences",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ActualDateUtc",
            table: "PersonalRecurringOccurrences",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ActualTransactionId",
            table: "PersonalRecurringOccurrences",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAtUtc",
            table: "PersonalRecurringOccurrences",
            type: "TEXT",
            nullable: false,
            defaultValue: new DateTime(
                2026, 9, 10, 8, 15, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<Guid>(
            name: "AccountId",
            table: "PersonalRecurringOccurrences",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AccountName",
            table: "PersonalRecurringOccurrences",
            type: "TEXT",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "KindCode",
            table: "PersonalRecurringOccurrences",
            type: "TEXT",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RuleName",
            table: "PersonalRecurringOccurrences",
            type: "TEXT",
            maxLength: 160,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CategoryCode",
            table: "PersonalRecurringOccurrences",
            type: "TEXT",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Counterparty",
            table: "PersonalRecurringOccurrences",
            type: "TEXT",
            maxLength: 200,
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE PersonalRecurringOccurrences
            SET
                AccountId = (
                    SELECT AccountId
                    FROM PersonalRecurringRules
                    WHERE PersonalRecurringRules.Id =
                          PersonalRecurringOccurrences.RecurringRuleId
                ),
                AccountName = (
                    SELECT PersonalFinancialAccounts.Name
                    FROM PersonalRecurringRules
                    JOIN PersonalFinancialAccounts
                      ON PersonalFinancialAccounts.Id =
                         PersonalRecurringRules.AccountId
                    WHERE PersonalRecurringRules.Id =
                          PersonalRecurringOccurrences.RecurringRuleId
                ),
                KindCode = (
                    SELECT KindCode
                    FROM PersonalRecurringRules
                    WHERE PersonalRecurringRules.Id =
                          PersonalRecurringOccurrences.RecurringRuleId
                ),
                RuleName = (
                    SELECT Name
                    FROM PersonalRecurringRules
                    WHERE PersonalRecurringRules.Id =
                          PersonalRecurringOccurrences.RecurringRuleId
                ),
                CategoryCode = (
                    SELECT CategoryCode
                    FROM PersonalRecurringRules
                    WHERE PersonalRecurringRules.Id =
                          PersonalRecurringOccurrences.RecurringRuleId
                ),
                Counterparty = (
                    SELECT Counterparty
                    FROM PersonalRecurringRules
                    WHERE PersonalRecurringRules.Id =
                          PersonalRecurringOccurrences.RecurringRuleId
                );
            """);

        migrationBuilder.CreateIndex(
            name: "IX_PersonalRecurringOccurrences_ActualTransactionId",
            table: "PersonalRecurringOccurrences",
            column: "ActualTransactionId");

        migrationBuilder.CreateIndex(
            name: "IX_PersonalRecurringOccurrences_AccountId",
            table: "PersonalRecurringOccurrences",
            column: "AccountId");

        migrationBuilder.AddForeignKey(
            name: "FK_PersonalRecurringOccurrences_PersonalFinancialTransactions_ActualTransactionId",
            table: "PersonalRecurringOccurrences",
            column: "ActualTransactionId",
            principalTable: "PersonalFinancialTransactions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_PersonalRecurringOccurrences_PersonalFinancialAccounts_AccountId",
            table: "PersonalRecurringOccurrences",
            column: "AccountId",
            principalTable: "PersonalFinancialAccounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "UpdatedAtUtc", "Version" },
            values: new object[]
            {
                new DateTime(
                    2026, 9, 10, 8, 15, 0, DateTimeKind.Utc),
                10
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_PersonalRecurringOccurrences_PersonalFinancialAccounts_AccountId",
            table: "PersonalRecurringOccurrences");

        migrationBuilder.DropForeignKey(
            name: "FK_PersonalRecurringOccurrences_PersonalFinancialTransactions_ActualTransactionId",
            table: "PersonalRecurringOccurrences");

        migrationBuilder.DropIndex(
            name: "IX_PersonalRecurringOccurrences_ActualTransactionId",
            table: "PersonalRecurringOccurrences");

        migrationBuilder.DropIndex(
            name: "IX_PersonalRecurringOccurrences_AccountId",
            table: "PersonalRecurringOccurrences");

        migrationBuilder.DropColumn(
            name: "ActualAmountMinor",
            table: "PersonalRecurringOccurrences");

        migrationBuilder.DropColumn(
            name: "ActualDateUtc",
            table: "PersonalRecurringOccurrences");

        migrationBuilder.DropColumn(
            name: "ActualTransactionId",
            table: "PersonalRecurringOccurrences");

        migrationBuilder.DropColumn(
            name: "UpdatedAtUtc",
            table: "PersonalRecurringOccurrences");

        migrationBuilder.DropColumn(
            name: "AccountId",
            table: "PersonalRecurringOccurrences");

        migrationBuilder.DropColumn(
            name: "AccountName",
            table: "PersonalRecurringOccurrences");

        migrationBuilder.DropColumn(
            name: "KindCode",
            table: "PersonalRecurringOccurrences");

        migrationBuilder.DropColumn(
            name: "RuleName",
            table: "PersonalRecurringOccurrences");

        migrationBuilder.DropColumn(
            name: "CategoryCode",
            table: "PersonalRecurringOccurrences");

        migrationBuilder.DropColumn(
            name: "Counterparty",
            table: "PersonalRecurringOccurrences");

        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "UpdatedAtUtc", "Version" },
            values: new object[]
            {
                new DateTime(
                    2026, 9, 10, 7, 46, 0, DateTimeKind.Utc),
                9
            });
    }
}
