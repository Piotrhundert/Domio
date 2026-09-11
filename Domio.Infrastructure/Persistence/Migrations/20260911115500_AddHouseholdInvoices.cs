
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

public partial class AddHouseholdInvoices : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "HouseholdInvoices",
            columns: table => new
            {
                Id = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                HouseholdId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                Supplier = table.Column<string>(
                    type: "TEXT",
                    maxLength: 200,
                    nullable: false),
                InvoiceNumber = table.Column<string>(
                    type: "TEXT",
                    maxLength: 100,
                    nullable: false),
                IssueDateUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false),
                DueDateUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false),
                GrossAmountMinor = table.Column<long>(
                    type: "INTEGER",
                    nullable: false),
                StatusCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 30,
                    nullable: false),
                CategoryCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 50,
                    nullable: false),
                UtilityInvoiceId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: true),
                CreatedByUserId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                CreatedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false),
                CancelledByUserId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: true),
                CancelledAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_HouseholdInvoices",
                    x => x.Id);

                table.ForeignKey(
                    name: "FK_HouseholdInvoices_Households_HouseholdId",
                    column: x => x.HouseholdId,
                    principalTable: "Households",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdInvoices_UserAccounts_CancelledByUserId",
                    column: x => x.CancelledByUserId,
                    principalTable: "UserAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdInvoices_UserAccounts_CreatedByUserId",
                    column: x => x.CreatedByUserId,
                    principalTable: "UserAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "HouseholdInvoicePayments",
            columns: table => new
            {
                Id = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                HouseholdId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                InvoiceId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                HouseholdAccountId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                CommandId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                AmountMinor = table.Column<long>(
                    type: "INTEGER",
                    nullable: false),
                PaidAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false),
                HouseholdEntryId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
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
                    "PK_HouseholdInvoicePayments",
                    x => x.Id);

                table.ForeignKey(
                    name: "FK_HouseholdInvoicePayments_HouseholdAccounts_HouseholdAccountId",
                    column: x => x.HouseholdAccountId,
                    principalTable: "HouseholdAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdInvoicePayments_HouseholdEntries_HouseholdEntryId",
                    column: x => x.HouseholdEntryId,
                    principalTable: "HouseholdEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdInvoicePayments_HouseholdInvoices_InvoiceId",
                    column: x => x.InvoiceId,
                    principalTable: "HouseholdInvoices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdInvoicePayments_Households_HouseholdId",
                    column: x => x.HouseholdId,
                    principalTable: "Households",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdInvoicePayments_UserAccounts_CreatedByUserId",
                    column: x => x.CreatedByUserId,
                    principalTable: "UserAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdInvoices_CancelledByUserId",
            table: "HouseholdInvoices",
            column: "CancelledByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdInvoices_CategoryCode",
            table: "HouseholdInvoices",
            column: "CategoryCode");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdInvoices_CreatedByUserId",
            table: "HouseholdInvoices",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdInvoices_DueDateUtc",
            table: "HouseholdInvoices",
            column: "DueDateUtc");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdInvoices_HouseholdId",
            table: "HouseholdInvoices",
            column: "HouseholdId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdInvoices_HouseholdId_Supplier_InvoiceNumber",
            table: "HouseholdInvoices",
            columns: new[]
            {
                "HouseholdId",
                "Supplier",
                "InvoiceNumber"
            },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdInvoices_StatusCode",
            table: "HouseholdInvoices",
            column: "StatusCode");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdInvoices_UtilityInvoiceId",
            table: "HouseholdInvoices",
            column: "UtilityInvoiceId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdInvoicePayments_CreatedByUserId",
            table: "HouseholdInvoicePayments",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdInvoicePayments_HouseholdAccountId",
            table: "HouseholdInvoicePayments",
            column: "HouseholdAccountId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdInvoicePayments_HouseholdEntryId",
            table: "HouseholdInvoicePayments",
            column: "HouseholdEntryId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdInvoicePayments_HouseholdId",
            table: "HouseholdInvoicePayments",
            column: "HouseholdId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdInvoicePayments_HouseholdId_CommandId",
            table: "HouseholdInvoicePayments",
            columns: new[]
            {
                "HouseholdId",
                "CommandId"
            },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdInvoicePayments_InvoiceId",
            table: "HouseholdInvoicePayments",
            column: "InvoiceId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdInvoicePayments_PaidAtUtc",
            table: "HouseholdInvoicePayments",
            column: "PaidAtUtc");

        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "UpdatedAtUtc", "Version" },
            values: new object[]
            {
                new DateTime(
                    2026, 9, 11, 11, 55, 0, DateTimeKind.Utc),
                15
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "HouseholdInvoicePayments");

        migrationBuilder.DropTable(
            name: "HouseholdInvoices");

        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "UpdatedAtUtc", "Version" },
            values: new object[]
            {
                new DateTime(
                    2026, 9, 11, 6, 36, 0, DateTimeKind.Utc),
                14
            });
    }
}
