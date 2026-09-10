using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

public partial class AddPersonalFinanceFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PersonalFinancialAccounts",
            columns: table => new
            {
                Id = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                OwnerPersonId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                Name = table.Column<string>(
                    type: "TEXT",
                    maxLength: 120,
                    nullable: false),
                AccountTypeCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 50,
                    nullable: false),
                CurrencyCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 3,
                    nullable: false),
                IsActive = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                CreatedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false),
                ArchivedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_PersonalFinancialAccounts",
                    x => x.Id);

                table.ForeignKey(
                    name: "FK_PersonalFinancialAccounts_People_OwnerPersonId",
                    column: x => x.OwnerPersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PersonalFinancialTransactions",
            columns: table => new
            {
                Id = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                AccountId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                OwnerPersonId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                KindCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 50,
                    nullable: false),
                AmountMinor = table.Column<long>(
                    type: "INTEGER",
                    nullable: false),
                OccurredAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false),
                Description = table.Column<string>(
                    type: "TEXT",
                    maxLength: 500,
                    nullable: true),
                CorrectsTransactionId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: true),
                CreatedByUserId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                CreatedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_PersonalFinancialTransactions",
                    x => x.Id);

                table.ForeignKey(
                    name: "FK_PersonalFinancialTransactions_PersonalFinancialAccounts_AccountId",
                    column: x => x.AccountId,
                    principalTable: "PersonalFinancialAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_PersonalFinancialTransactions_People_OwnerPersonId",
                    column: x => x.OwnerPersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_PersonalFinancialTransactions_UserAccounts_CreatedByUserId",
                    column: x => x.CreatedByUserId,
                    principalTable: "UserAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_PersonalFinancialTransactions_PersonalFinancialTransactions_CorrectsTransactionId",
                    column: x => x.CorrectsTransactionId,
                    principalTable: "PersonalFinancialTransactions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PersonalFinancialAccounts_OwnerPersonId",
            table: "PersonalFinancialAccounts",
            column: "OwnerPersonId");

        migrationBuilder.CreateIndex(
            name: "IX_PersonalFinancialAccounts_OwnerPersonId_IsActive",
            table: "PersonalFinancialAccounts",
            columns: new[]
            {
                "OwnerPersonId",
                "IsActive"
            });

        migrationBuilder.CreateIndex(
            name: "IX_PersonalFinancialTransactions_AccountId",
            table: "PersonalFinancialTransactions",
            column: "AccountId");

        migrationBuilder.CreateIndex(
            name: "IX_PersonalFinancialTransactions_CorrectsTransactionId",
            table: "PersonalFinancialTransactions",
            column: "CorrectsTransactionId");

        migrationBuilder.CreateIndex(
            name: "IX_PersonalFinancialTransactions_CreatedByUserId",
            table: "PersonalFinancialTransactions",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_PersonalFinancialTransactions_OwnerPersonId",
            table: "PersonalFinancialTransactions",
            column: "OwnerPersonId");

        migrationBuilder.CreateIndex(
            name: "IX_PersonalFinancialTransactions_OwnerPersonId_OccurredAtUtc",
            table: "PersonalFinancialTransactions",
            columns: new[]
            {
                "OwnerPersonId",
                "OccurredAtUtc"
            });

        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[]
            {
                "UpdatedAtUtc",
                "Version"
            },
            values: new object[]
            {
                new DateTime(
                    2026, 9, 10, 6, 58, 0, DateTimeKind.Utc),
                8
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PersonalFinancialTransactions");

        migrationBuilder.DropTable(
            name: "PersonalFinancialAccounts");

        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[]
            {
                "UpdatedAtUtc",
                "Version"
            },
            values: new object[]
            {
                new DateTime(
                    2026, 9, 9, 8, 41, 0, DateTimeKind.Utc),
                7
            });
    }
}
