using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DomioDbContext))]
[Migration("20260925073500_AddNotificationMessageTemplates")]
public sealed class AddNotificationMessageTemplates : Migration
{
    protected override void Up(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "NotificationMessageTemplates",
            columns: table => new
            {
                CategoryCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 50,
                    nullable: false),
                SubjectTemplate = table.Column<string>(
                    type: "TEXT",
                    maxLength: 500,
                    nullable: false),
                BodyTemplate = table.Column<string>(
                    type: "TEXT",
                    maxLength: 4000,
                    nullable: false),
                UpdatedByUserId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_NotificationMessageTemplates",
                    x => x.CategoryCode);

                table.ForeignKey(
                    "FK_NotificationMessageTemplates_UserAccounts_UpdatedByUserId",
                    x => x.UpdatedByUserId,
                    "UserAccounts",
                    "Id",
                    onDelete:
                        ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name:
                "IX_NotificationMessageTemplates_UpdatedByUserId",
            table: "NotificationMessageTemplates",
            column: "UpdatedByUserId");

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-25T07:35:00.0000000Z',
                "Version" = 29
            WHERE "Id" = 1;
            """);
    }

    protected override void Down(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "NotificationMessageTemplates");

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-25T07:15:00.0000000Z',
                "Version" = 28
            WHERE "Id" = 1;
            """);
    }
}
