using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DomioDbContext))]
[Migration("20260924123000_AddNotificationCenter")]
public sealed class AddNotificationCenter : Migration
{
    protected override void Up(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "UserNotifications",
            columns: table => new
            {
                Id = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                UserId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                EventCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 100,
                    nullable: false),
                CategoryCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 50,
                    nullable: false),
                SeverityCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 30,
                    nullable: false),
                Title = table.Column<string>(
                    type: "TEXT",
                    maxLength: 180,
                    nullable: false),
                Message = table.Column<string>(
                    type: "TEXT",
                    maxLength: 1000,
                    nullable: false),
                LinkUrl = table.Column<string>(
                    type: "TEXT",
                    maxLength: 500,
                    nullable: true),
                SourceType = table.Column<string>(
                    type: "TEXT",
                    maxLength: 100,
                    nullable: true),
                SourceId = table.Column<string>(
                    type: "TEXT",
                    maxLength: 200,
                    nullable: true),
                DedupeKey = table.Column<string>(
                    type: "TEXT",
                    maxLength: 200,
                    nullable: true),
                ShowInInbox = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                IsRead = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                ReadAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: true),
                CreatedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_UserNotifications",
                    x => x.Id);

                table.ForeignKey(
                    "FK_UserNotifications_UserAccounts_UserId",
                    x => x.UserId,
                    "UserAccounts",
                    "Id",
                    onDelete:
                        ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name:
                "IX_UserNotifications_UserId_CreatedAtUtc",
            table: "UserNotifications",
            columns:
            [
                "UserId",
                "CreatedAtUtc"
            ]);

        migrationBuilder.CreateIndex(
            name:
                "IX_UserNotifications_UserId_ShowInInbox_IsRead",
            table: "UserNotifications",
            columns:
            [
                "UserId",
                "ShowInInbox",
                "IsRead"
            ]);

        migrationBuilder.CreateIndex(
            name:
                "IX_UserNotifications_CategoryCode",
            table: "UserNotifications",
            column: "CategoryCode");

        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX
                "IX_UserNotifications_UserId_DedupeKey"
            ON "UserNotifications"
                ("UserId", "DedupeKey")
            WHERE "DedupeKey" IS NOT NULL;
            """);

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-24T11:30:00.0000000Z',
                "Version" = 26
            WHERE "Id" = 1;
            """);
    }

    protected override void Down(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "UserNotifications");

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-24T08:30:00.0000000Z',
                "Version" = 25
            WHERE "Id" = 1;
            """);
    }
}
