using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DomioDbContext))]
[Migration("20260923123000_AddFamilySharedAccounts")]
public sealed class AddFamilySharedAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FamilySharedAccounts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                FamilyGroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                PersonalAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                OwnerPersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                CoOwnerPersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                ClosedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FamilySharedAccounts", x => x.Id);
                table.ForeignKey(
                    "FK_FamilySharedAccounts_FamilyGroups_FamilyGroupId",
                    x => x.FamilyGroupId,
                    "FamilyGroups",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilySharedAccounts_PersonalFinancialAccounts_PersonalAccountId",
                    x => x.PersonalAccountId,
                    "PersonalFinancialAccounts",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilySharedAccounts_People_OwnerPersonId",
                    x => x.OwnerPersonId,
                    "People",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilySharedAccounts_People_CoOwnerPersonId",
                    x => x.CoOwnerPersonId,
                    "People",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    "FK_FamilySharedAccounts_UserAccounts_CreatedByUserId",
                    x => x.CreatedByUserId,
                    "UserAccounts",
                    "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            "IX_FamilySharedAccounts_FamilyGroupId",
            "FamilySharedAccounts",
            "FamilyGroupId");

        migrationBuilder.CreateIndex(
            "IX_FamilySharedAccounts_OwnerPersonId",
            "FamilySharedAccounts",
            "OwnerPersonId");

        migrationBuilder.CreateIndex(
            "IX_FamilySharedAccounts_CoOwnerPersonId",
            "FamilySharedAccounts",
            "CoOwnerPersonId");

        migrationBuilder.CreateIndex(
            name: "IX_FamilySharedAccounts_PersonalAccountId",
            table: "FamilySharedAccounts",
            column: "PersonalAccountId",
            unique: true);

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-23T10:30:00.0000000Z',
                "Version" = 22
            WHERE "Id" = 1;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FamilySharedAccounts");

        migrationBuilder.Sql(
            """
            UPDATE "SchemaVersions"
            SET "UpdatedAtUtc" = '2026-09-21T20:05:00.0000000Z',
                "Version" = 21
            WHERE "Id" = 1;
            """);
    }
}
