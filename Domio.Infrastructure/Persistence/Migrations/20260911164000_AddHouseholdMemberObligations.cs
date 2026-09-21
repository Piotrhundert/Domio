using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

public partial class AddHouseholdMemberObligations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "HouseholdMemberObligations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                HouseholdMemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                SourceTypeCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                SourceInvoiceId = table.Column<Guid>(type: "TEXT", nullable: true),
                Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                AmountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                PaidAmountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                DueDateUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                StatusCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                TargetHouseholdAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CancelledByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                CancelledAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HouseholdMemberObligations", x => x.Id);
                table.ForeignKey("FK_HouseholdMemberObligations_HouseholdAccounts_TargetHouseholdAccountId", x => x.TargetHouseholdAccountId, "HouseholdAccounts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_HouseholdMemberObligations_HouseholdInvoices_SourceInvoiceId", x => x.SourceInvoiceId, "HouseholdInvoices", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_HouseholdMemberObligations_HouseholdMembers_HouseholdMemberId", x => x.HouseholdMemberId, "HouseholdMembers", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_HouseholdMemberObligations_Households_HouseholdId", x => x.HouseholdId, "Households", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_HouseholdMemberObligations_UserAccounts_CancelledByUserId", x => x.CancelledByUserId, "UserAccounts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_HouseholdMemberObligations_UserAccounts_CreatedByUserId", x => x.CreatedByUserId, "UserAccounts", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "HouseholdMemberObligationPaymentRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                HouseholdMemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                ObligationId = table.Column<Guid>(type: "TEXT", nullable: false),
                SourcePersonalAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                TargetHouseholdAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                AmountMinor = table.Column<long>(type: "INTEGER", nullable: false),
                StatusCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                SubmittedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                SubmittedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                ReviewedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                ReviewedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                ReviewNote = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                PersonalTransactionId = table.Column<Guid>(type: "TEXT", nullable: true),
                HouseholdEntryId = table.Column<Guid>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HouseholdMemberObligationPaymentRequests", x => x.Id);
                table.ForeignKey("FK_HouseholdMemberObligationPaymentRequests_HouseholdAccounts_TargetHouseholdAccountId", x => x.TargetHouseholdAccountId, "HouseholdAccounts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_HouseholdMemberObligationPaymentRequests_HouseholdEntries_HouseholdEntryId", x => x.HouseholdEntryId, "HouseholdEntries", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_HouseholdMemberObligationPaymentRequests_HouseholdMemberObligations_ObligationId", x => x.ObligationId, "HouseholdMemberObligations", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_HouseholdMemberObligationPaymentRequests_HouseholdMembers_HouseholdMemberId", x => x.HouseholdMemberId, "HouseholdMembers", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_HouseholdMemberObligationPaymentRequests_Households_HouseholdId", x => x.HouseholdId, "Households", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_HouseholdMemberObligationPaymentRequests_PersonalFinancialAccounts_SourcePersonalAccountId", x => x.SourcePersonalAccountId, "PersonalFinancialAccounts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_HouseholdMemberObligationPaymentRequests_PersonalFinancialTransactions_PersonalTransactionId", x => x.PersonalTransactionId, "PersonalFinancialTransactions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_HouseholdMemberObligationPaymentRequests_UserAccounts_ReviewedByUserId", x => x.ReviewedByUserId, "UserAccounts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_HouseholdMemberObligationPaymentRequests_UserAccounts_SubmittedByUserId", x => x.SubmittedByUserId, "UserAccounts", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_HouseholdMemberObligations_CancelledByUserId", "HouseholdMemberObligations", "CancelledByUserId");
        migrationBuilder.CreateIndex("IX_HouseholdMemberObligations_CreatedByUserId", "HouseholdMemberObligations", "CreatedByUserId");
        migrationBuilder.CreateIndex("IX_HouseholdMemberObligations_DueDateUtc", "HouseholdMemberObligations", "DueDateUtc");
        migrationBuilder.CreateIndex("IX_HouseholdMemberObligations_HouseholdId", "HouseholdMemberObligations", "HouseholdId");
        migrationBuilder.CreateIndex("IX_HouseholdMemberObligations_HouseholdMemberId", "HouseholdMemberObligations", "HouseholdMemberId");
        migrationBuilder.CreateIndex("IX_HouseholdMemberObligations_SourceInvoiceId", "HouseholdMemberObligations", "SourceInvoiceId");
        migrationBuilder.CreateIndex("IX_HouseholdMemberObligations_StatusCode", "HouseholdMemberObligations", "StatusCode");
        migrationBuilder.CreateIndex("IX_HouseholdMemberObligations_TargetHouseholdAccountId", "HouseholdMemberObligations", "TargetHouseholdAccountId");
        migrationBuilder.CreateIndex("IX_HouseholdMemberObligationPaymentRequests_HouseholdEntryId", "HouseholdMemberObligationPaymentRequests", "HouseholdEntryId", unique: true);
        migrationBuilder.CreateIndex("IX_HouseholdMemberObligationPaymentRequests_HouseholdId", "HouseholdMemberObligationPaymentRequests", "HouseholdId");
        migrationBuilder.CreateIndex("IX_HouseholdMemberObligationPaymentRequests_HouseholdMemberId", "HouseholdMemberObligationPaymentRequests", "HouseholdMemberId");
        migrationBuilder.CreateIndex("IX_HouseholdMemberObligationPaymentRequests_ObligationId", "HouseholdMemberObligationPaymentRequests", "ObligationId");
        migrationBuilder.CreateIndex("IX_HouseholdMemberObligationPaymentRequests_PersonalTransactionId", "HouseholdMemberObligationPaymentRequests", "PersonalTransactionId", unique: true);
        migrationBuilder.CreateIndex("IX_HouseholdMemberObligationPaymentRequests_ReviewedByUserId", "HouseholdMemberObligationPaymentRequests", "ReviewedByUserId");
        migrationBuilder.CreateIndex("IX_HouseholdMemberObligationPaymentRequests_SourcePersonalAccountId", "HouseholdMemberObligationPaymentRequests", "SourcePersonalAccountId");
        migrationBuilder.CreateIndex("IX_HouseholdMemberObligationPaymentRequests_StatusCode", "HouseholdMemberObligationPaymentRequests", "StatusCode");
        migrationBuilder.CreateIndex("IX_HouseholdMemberObligationPaymentRequests_SubmittedAtUtc", "HouseholdMemberObligationPaymentRequests", "SubmittedAtUtc");
        migrationBuilder.CreateIndex("IX_HouseholdMemberObligationPaymentRequests_SubmittedByUserId", "HouseholdMemberObligationPaymentRequests", "SubmittedByUserId");
        migrationBuilder.CreateIndex("IX_HouseholdMemberObligationPaymentRequests_TargetHouseholdAccountId", "HouseholdMemberObligationPaymentRequests", "TargetHouseholdAccountId");

        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "UpdatedAtUtc", "Version" },
            values: new object[] { new DateTime(2026, 9, 11, 16, 40, 0, DateTimeKind.Utc), 17 });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "HouseholdMemberObligationPaymentRequests");
        migrationBuilder.DropTable(name: "HouseholdMemberObligations");

        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "UpdatedAtUtc", "Version" },
            values: new object[] { new DateTime(2026, 9, 11, 12, 50, 0, DateTimeKind.Utc), 16 });
    }
}
