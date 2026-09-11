using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

public partial class AddHouseholdContributionPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "HouseholdContributionPaymentRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                HouseholdId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                HouseholdMemberId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                ObligationId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                SourcePersonalAccountId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                TargetHouseholdAccountId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                AmountMinor = table.Column<long>(
                    type: "INTEGER",
                    nullable: false),
                StatusCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 30,
                    nullable: false),
                SubmittedByUserId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                SubmittedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false),
                ReviewedByUserId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: true),
                ReviewedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: true),
                ReviewNote = table.Column<string>(
                    type: "TEXT",
                    maxLength: 500,
                    nullable: true),
                PersonalTransactionId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: true),
                HouseholdEntryId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_HouseholdContributionPaymentRequests",
                    x => x.Id);

                table.ForeignKey(
                    name: "FK_HouseholdContributionPaymentRequests_HouseholdAccounts_TargetHouseholdAccountId",
                    column: x => x.TargetHouseholdAccountId,
                    principalTable: "HouseholdAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdContributionPaymentRequests_HouseholdContributionObligations_ObligationId",
                    column: x => x.ObligationId,
                    principalTable: "HouseholdContributionObligations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdContributionPaymentRequests_HouseholdEntries_HouseholdEntryId",
                    column: x => x.HouseholdEntryId,
                    principalTable: "HouseholdEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdContributionPaymentRequests_HouseholdMembers_HouseholdMemberId",
                    column: x => x.HouseholdMemberId,
                    principalTable: "HouseholdMembers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdContributionPaymentRequests_Households_HouseholdId",
                    column: x => x.HouseholdId,
                    principalTable: "Households",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdContributionPaymentRequests_PersonalFinancialAccounts_SourcePersonalAccountId",
                    column: x => x.SourcePersonalAccountId,
                    principalTable: "PersonalFinancialAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdContributionPaymentRequests_PersonalFinancialTransactions_PersonalTransactionId",
                    column: x => x.PersonalTransactionId,
                    principalTable: "PersonalFinancialTransactions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdContributionPaymentRequests_UserAccounts_ReviewedByUserId",
                    column: x => x.ReviewedByUserId,
                    principalTable: "UserAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdContributionPaymentRequests_UserAccounts_SubmittedByUserId",
                    column: x => x.SubmittedByUserId,
                    principalTable: "UserAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionPaymentRequests_HouseholdEntryId",
            table: "HouseholdContributionPaymentRequests",
            column: "HouseholdEntryId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionPaymentRequests_HouseholdId",
            table: "HouseholdContributionPaymentRequests",
            column: "HouseholdId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionPaymentRequests_HouseholdMemberId",
            table: "HouseholdContributionPaymentRequests",
            column: "HouseholdMemberId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionPaymentRequests_ObligationId",
            table: "HouseholdContributionPaymentRequests",
            column: "ObligationId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionPaymentRequests_PersonalTransactionId",
            table: "HouseholdContributionPaymentRequests",
            column: "PersonalTransactionId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionPaymentRequests_ReviewedByUserId",
            table: "HouseholdContributionPaymentRequests",
            column: "ReviewedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionPaymentRequests_SourcePersonalAccountId",
            table: "HouseholdContributionPaymentRequests",
            column: "SourcePersonalAccountId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionPaymentRequests_StatusCode",
            table: "HouseholdContributionPaymentRequests",
            column: "StatusCode");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionPaymentRequests_SubmittedAtUtc",
            table: "HouseholdContributionPaymentRequests",
            column: "SubmittedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionPaymentRequests_SubmittedByUserId",
            table: "HouseholdContributionPaymentRequests",
            column: "SubmittedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdContributionPaymentRequests_TargetHouseholdAccountId",
            table: "HouseholdContributionPaymentRequests",
            column: "TargetHouseholdAccountId");

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

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "HouseholdContributionPaymentRequests");

        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "UpdatedAtUtc", "Version" },
            values: new object[]
            {
                new DateTime(
                    2026, 9, 10, 12, 16, 0, DateTimeKind.Utc),
                13
            });
    }
}
