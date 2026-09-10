using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

public partial class AddPersonalTransactionMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CategoryCode",
            table: "PersonalFinancialTransactions",
            type: "TEXT",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Counterparty",
            table: "PersonalFinancialTransactions",
            type: "TEXT",
            maxLength: 200,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_PersonalFinancialTransactions_CategoryCode",
            table: "PersonalFinancialTransactions",
            column: "CategoryCode");

        migrationBuilder.Sql(
            """
            UPDATE PersonalFinancialTransactions
            SET CategoryCode = 'OtherIncome'
            WHERE KindCode = 'Income'
              AND CategoryCode IS NULL;

            UPDATE PersonalFinancialTransactions
            SET CategoryCode = 'OtherExpense'
            WHERE KindCode = 'Expense'
              AND CategoryCode IS NULL;

            UPDATE PersonalFinancialTransactions
            SET
                CategoryCode = COALESCE(
                    (
                        SELECT pro.CategoryCode
                        FROM PersonalRecurringOccurrences pro
                        WHERE pro.ActualTransactionId =
                              PersonalFinancialTransactions.Id
                        LIMIT 1
                    ),
                    CategoryCode
                ),
                Counterparty = COALESCE(
                    (
                        SELECT pro.Counterparty
                        FROM PersonalRecurringOccurrences pro
                        WHERE pro.ActualTransactionId =
                              PersonalFinancialTransactions.Id
                        LIMIT 1
                    ),
                    Counterparty
                )
            WHERE EXISTS (
                SELECT 1
                FROM PersonalRecurringOccurrences pro
                WHERE pro.ActualTransactionId =
                      PersonalFinancialTransactions.Id
            );
            """);

        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "UpdatedAtUtc", "Version" },
            values: new object[]
            {
                new DateTime(
                    2026, 9, 10, 10, 56, 0, DateTimeKind.Utc),
                11
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PersonalFinancialTransactions_CategoryCode",
            table: "PersonalFinancialTransactions");

        migrationBuilder.DropColumn(
            name: "CategoryCode",
            table: "PersonalFinancialTransactions");

        migrationBuilder.DropColumn(
            name: "Counterparty",
            table: "PersonalFinancialTransactions");

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
}
