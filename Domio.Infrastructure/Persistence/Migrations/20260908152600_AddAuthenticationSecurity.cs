using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

public partial class AddAuthenticationSecurity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "FailedLoginAttempts",
            table: "UserAccounts",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "LockoutEndUtc",
            table: "UserAccounts",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "PasswordChangedAtUtc",
            table: "UserAccounts",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PasswordHash",
            table: "UserAccounts",
            type: "TEXT",
            maxLength: 512,
            nullable: true);

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
                    2026, 9, 8, 15, 16, 0, DateTimeKind.Utc),
                3
            });

        migrationBuilder.DropColumn(
            name: "FailedLoginAttempts",
            table: "UserAccounts");

        migrationBuilder.DropColumn(
            name: "LockoutEndUtc",
            table: "UserAccounts");

        migrationBuilder.DropColumn(
            name: "PasswordChangedAtUtc",
            table: "UserAccounts");

        migrationBuilder.DropColumn(
            name: "PasswordHash",
            table: "UserAccounts");
    }
}
