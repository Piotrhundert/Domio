using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

public partial class AddHouseholdFinanceFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Households",
            columns: table => new
            {
                Id = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                Name = table.Column<string>(
                    type: "TEXT",
                    maxLength: 160,
                    nullable: false),
                CurrencyCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 3,
                    nullable: false),
                IsActive = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                CreatedByUserId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                CreatedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_Households",
                    x => x.Id);

                table.ForeignKey(
                    name: "FK_Households_UserAccounts_CreatedByUserId",
                    column: x => x.CreatedByUserId,
                    principalTable: "UserAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "HouseholdAccounts",
            columns: table => new
            {
                Id = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                HouseholdId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                Name = table.Column<string>(
                    type: "TEXT",
                    maxLength: 120,
                    nullable: false),
                AccountTypeCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 50,
                    nullable: false),
                CurrencyCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 3,
                    nullable: false),
                IsActive = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                CreatedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false),
                ArchivedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_HouseholdAccounts",
                    x => x.Id);

                table.ForeignKey(
                    name: "FK_HouseholdAccounts_Households_HouseholdId",
                    column: x => x.HouseholdId,
                    principalTable: "Households",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "HouseholdMembers",
            columns: table => new
            {
                Id = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                HouseholdId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                PersonId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                IsActive = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false),
                JoinedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false),
                LeftAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_HouseholdMembers",
                    x => x.Id);

                table.ForeignKey(
                    name: "FK_HouseholdMembers_Households_HouseholdId",
                    column: x => x.HouseholdId,
                    principalTable: "Households",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdMembers_People_PersonId",
                    column: x => x.PersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "HouseholdEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                HouseholdId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                AccountId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                EntryTypeCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 50,
                    nullable: false),
                AmountMinor = table.Column<long>(
                    type: "INTEGER",
                    nullable: false),
                OccurredAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false),
                CategoryCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 50,
                    nullable: true),
                Description = table.Column<string>(
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
                CorrectsEntryId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: true),
                CreatedByUserId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                CreatedAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_HouseholdEntries",
                    x => x.Id);

                table.ForeignKey(
                    name: "FK_HouseholdEntries_HouseholdAccounts_AccountId",
                    column: x => x.AccountId,
                    principalTable: "HouseholdAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdEntries_HouseholdEntries_CorrectsEntryId",
                    column: x => x.CorrectsEntryId,
                    principalTable: "HouseholdEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdEntries_Households_HouseholdId",
                    column: x => x.HouseholdId,
                    principalTable: "Households",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_HouseholdEntries_UserAccounts_CreatedByUserId",
                    column: x => x.CreatedByUserId,
                    principalTable: "UserAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Households_CreatedByUserId",
            table: "Households",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_Households_IsActive",
            table: "Households",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdMembers_HouseholdId",
            table: "HouseholdMembers",
            column: "HouseholdId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdMembers_PersonId",
            table: "HouseholdMembers",
            column: "PersonId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdMembers_HouseholdId_PersonId",
            table: "HouseholdMembers",
            columns: new[] { "HouseholdId", "PersonId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdAccounts_HouseholdId",
            table: "HouseholdAccounts",
            column: "HouseholdId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdAccounts_HouseholdId_IsActive",
            table: "HouseholdAccounts",
            columns: new[] { "HouseholdId", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdEntries_AccountId",
            table: "HouseholdEntries",
            column: "AccountId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdEntries_CategoryCode",
            table: "HouseholdEntries",
            column: "CategoryCode");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdEntries_CorrectsEntryId",
            table: "HouseholdEntries",
            column: "CorrectsEntryId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdEntries_CreatedByUserId",
            table: "HouseholdEntries",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdEntries_HouseholdId",
            table: "HouseholdEntries",
            column: "HouseholdId");

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdEntries_HouseholdId_OccurredAtUtc",
            table: "HouseholdEntries",
            columns: new[] { "HouseholdId", "OccurredAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_HouseholdEntries_SourceType_SourceId",
            table: "HouseholdEntries",
            columns: new[] { "SourceType", "SourceId" });

        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "UpdatedAtUtc", "Version" },
            values: new object[]
            {
                new DateTime(
                    2026, 9, 10, 11, 47, 0, DateTimeKind.Utc),
                12
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "HouseholdEntries");

        migrationBuilder.DropTable(
            name: "HouseholdMembers");

        migrationBuilder.DropTable(
            name: "HouseholdAccounts");

        migrationBuilder.DropTable(
            name: "Households");

        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "UpdatedAtUtc", "Version" },
            values: new object[]
            {
                new DateTime(
                    2026, 9, 10, 10, 56, 0, DateTimeKind.Utc),
                11
            });
    }
}
