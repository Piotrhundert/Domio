using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

public partial class AddUserAccountEmail : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Email",
            table: "UserAccounts",
            type: "TEXT",
            maxLength: 254,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "NormalizedEmail",
            table: "UserAccounts",
            type: "TEXT",
            maxLength: 254,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserAccounts_NormalizedEmail",
            table: "UserAccounts",
            column: "NormalizedEmail",
            unique: true);

        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "UpdatedAtUtc", "Version" },
            values: new object[]
            {
                new DateTime(
                    2026, 9, 9, 6, 28, 0, DateTimeKind.Utc),
                5
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "UpdatedAtUtc", "Version" },
            values: new object[]
            {
                new DateTime(
                    2026, 9, 8, 15, 26, 0, DateTimeKind.Utc),
                4
            });

        migrationBuilder.DropIndex(
            name: "IX_UserAccounts_NormalizedEmail",
            table: "UserAccounts");

        migrationBuilder.DropColumn(
            name: "Email",
            table: "UserAccounts");

        migrationBuilder.DropColumn(
            name: "NormalizedEmail",
            table: "UserAccounts");
    }
}
