using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

public partial class AddRolePermissionConfiguration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PermissionConfigurationJson",
            table: "RoleDefinitions",
            type: "TEXT",
            maxLength: 16000,
            nullable: true);

        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "UpdatedAtUtc", "Version" },
            values: new object[]
            {
                new DateTime(
                    2026, 9, 9, 7, 13, 0, DateTimeKind.Utc),
                6
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
                    2026, 9, 9, 6, 28, 0, DateTimeKind.Utc),
                5
            });

        migrationBuilder.DropColumn(
            name: "PermissionConfigurationJson",
            table: "RoleDefinitions");
    }
}
