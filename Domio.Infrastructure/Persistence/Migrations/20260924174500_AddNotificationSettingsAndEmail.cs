using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DomioDbContext))]
[Migration("20260924174500_AddNotificationSettingsAndEmail")]
public sealed class AddNotificationSettingsAndEmail : Migration
{
    protected override void Up(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "NotificationUserSettings",
            columns: table => new
            {
                UserId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                InAppEnabled = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                EmailEnabled = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                EmailAddress = table.Column<string>(
                    type: "TEXT",
                    maxLength: 320,
                    nullable: true),
                Reminder7Days = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                Reminder3Days = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                Reminder1Day = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                ReminderDueDay = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                ReminderOverdue = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                NotifySystem = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                NotifyPersonalFinance = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                NotifyHousehold = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                NotifyContributions = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                NotifyInvoices = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                NotifyFamily = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                NotifyGoals = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                NotifyUsers = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                EmailEnabledAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: true),
                UpdatedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_NotificationUserSettings",
                    x => x.UserId);

                table.ForeignKey(
                    "FK_NotificationUserSettings_UserAccounts_UserId",
                    x => x.UserId,
                    "UserAccounts",
                    "Id",
                    onDelete:
                        ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "NotificationEmailSettings",
            columns: table => new
            {
                Id = table.Column<int>(
                    type: "INTEGER",
                    nullable: false),
                IsEnabled = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                SenderName = table.Column<string>(
                    type: "TEXT",
                    maxLength: 160,
                    nullable: false),
                SenderEmail = table.Column<string>(
                    type: "TEXT",
                    maxLength: 320,
                    nullable: false),
                SmtpHost = table.Column<string>(
                    type: "TEXT",
                    maxLength: 255,
                    nullable: false),
                SmtpPort = table.Column<int>(
                    type: "INTEGER",
                    nullable: false),
                SmtpUsername = table.Column<string>(
                    type: "TEXT",
                    maxLength: 255,
                    nullable: false),
                ProtectedPassword = table.Column<string>(
                    type: "TEXT",
                    nullable: true),
                UseSsl = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                ApplicationBaseUrl = table.Column<string>(
                    type: "TEXT",
                    maxLength: 500,
                    nullable: true),
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
                    "PK_NotificationEmailSettings",
                    x => x.Id);

                table.ForeignKey(
                    "FK_NotificationEmailSettings_UserAccounts_UpdatedByUserId",
                    x => x.UpdatedByUserId,
                    "UserAccounts",
                    "Id",
                    onDelete:
                        ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name:
                "IX_NotificationEmailSettings_UpdatedByUserId",
            table: "NotificationEmailSettings",
            column: "UpdatedByUserId");

        migrationBuilder.CreateTable(
            name: "NotificationEmailDeliveries",
            columns: table => new
            {
                Id = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                NotificationId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: true),
                RecipientUserId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: true),
                RecipientEmail = table.Column<string>(
                    type: "TEXT",
                    maxLength: 320,
                    nullable: false),
                Subject = table.Column<string>(
                    type: "TEXT",
                    maxLength: 300,
                    nullable: false),
                Body = table.Column<string>(
                    type: "TEXT",
                    maxLength: 4000,
                    nullable: false),
                StatusCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 30,
                    nullable: false),
                AttemptCount = table.Column<int>(
                    type: "INTEGER",
                    nullable: false),
                CreatedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false),
                LastAttemptAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: true),
                SentAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: true),
                LastError = table.Column<string>(
                    type: "TEXT",
                    maxLength: 1000,
                    nullable: true),
                IsTest = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_NotificationEmailDeliveries",
                    x => x.Id);

                table.ForeignKey(
                    "FK_NotificationEmailDeliveries_UserNotifications_NotificationId",
                    x => x.NotificationId,
                    "UserNotifications",
                    "Id",
                    onDelete:
                        ReferentialAction.Restrict);

                table.ForeignKey(
                    "FK_NotificationEmailDeliveries_UserAccounts_RecipientUserId",
                    x => x.RecipientUserId,
                    "UserAccounts",
                    "Id",
                    onDelete:
                        ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name:
                "IX_NotificationEmailDeliveries_RecipientUserId_CreatedAtUtc",
            table: "NotificationEmailDeliveries",
            columns:
            [
                "RecipientUserId",
                "CreatedAtUtc"
            ]);

        migrationBuilder.CreateIndex(
            name:
                "IX_NotificationEmailDeliveries_StatusCode_AttemptCount",
            table: "NotificationEmailDeliveries",
            columns:
            [
                "StatusCode",
                "AttemptCount"
            ]);

        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX
                "IX_NotificationEmailDeliveries_NotificationId"
            ON "NotificationEmailDeliveries"
                ("NotificationId")
            WHERE "NotificationId" IS NOT NULL;
            """);

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-24T15:45:00.0000000Z',
                "Version" = 27
            WHERE "Id" = 1;
            """);
    }

    protected override void Down(
        MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "NotificationEmailDeliveries");

        migrationBuilder.DropTable(
            name: "NotificationEmailSettings");

        migrationBuilder.DropTable(
            name: "NotificationUserSettings");

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-24T11:30:00.0000000Z',
                "Version" = 26
            WHERE "Id" = 1;
            """);
    }
}
