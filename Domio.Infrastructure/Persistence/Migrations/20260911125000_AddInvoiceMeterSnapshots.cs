
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

public partial class AddInvoiceMeterSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "BillingPeriodFromUtc",
            table: "HouseholdInvoices",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "BillingPeriodToUtc",
            table: "HouseholdInvoices",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MainMeterCurrentReading",
            table: "HouseholdInvoices",
            type: "TEXT",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MainMeterNumber",
            table: "HouseholdInvoices",
            type: "TEXT",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MainMeterPreviousReading",
            table: "HouseholdInvoices",
            type: "TEXT",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MainMeterUnit",
            table: "HouseholdInvoices",
            type: "TEXT",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SubmeterReadingsSnapshot",
            table: "HouseholdInvoices",
            type: "TEXT",
            maxLength: 4000,
            nullable: true);

        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "UpdatedAtUtc", "Version" },
            values: new object[]
            {
                new DateTime(
                    2026, 9, 11, 12, 50, 0, DateTimeKind.Utc),
                16
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BillingPeriodFromUtc",
            table: "HouseholdInvoices");

        migrationBuilder.DropColumn(
            name: "BillingPeriodToUtc",
            table: "HouseholdInvoices");

        migrationBuilder.DropColumn(
            name: "MainMeterCurrentReading",
            table: "HouseholdInvoices");

        migrationBuilder.DropColumn(
            name: "MainMeterNumber",
            table: "HouseholdInvoices");

        migrationBuilder.DropColumn(
            name: "MainMeterPreviousReading",
            table: "HouseholdInvoices");

        migrationBuilder.DropColumn(
            name: "MainMeterUnit",
            table: "HouseholdInvoices");

        migrationBuilder.DropColumn(
            name: "SubmeterReadingsSnapshot",
            table: "HouseholdInvoices");

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
}
