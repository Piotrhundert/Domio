using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DomioDbContext))]
[Migration("20260925071500_AddNotificationPollingInterval")]
public sealed class AddNotificationPollingInterval : Migration
{
    protected override void Up(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "PollIntervalMinutes",
            table: "NotificationEmailSettings",
            type: "INTEGER",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.Sql(
            """
            UPDATE "NotificationEmailSettings"
            SET "PollIntervalMinutes" = 1
            WHERE "PollIntervalMinutes" < 1
               OR "PollIntervalMinutes" > 60;
            """);

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-25T07:15:00.0000000Z',
                "Version" = 28
            WHERE "Id" = 1;
            """);
    }

    protected override void Down(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PollIntervalMinutes",
            table: "NotificationEmailSettings");

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-24T15:45:00.0000000Z',
                "Version" = 27
            WHERE "Id" = 1;
            """);
    }
}
