using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

public partial class AddUsersAndRoles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "People",
            columns: table => new
            {
                Id = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                FirstName = table.Column<string>(
                    type: "TEXT",
                    maxLength: 100,
                    nullable: false),
                LastName = table.Column<string>(
                    type: "TEXT",
                    maxLength: 100,
                    nullable: false),
                DisplayName = table.Column<string>(
                    type: "TEXT",
                    maxLength: 200,
                    nullable: true),
                Email = table.Column<string>(
                    type: "TEXT",
                    maxLength: 254,
                    nullable: true),
                Phone = table.Column<string>(
                    type: "TEXT",
                    maxLength: 50,
                    nullable: true),
                IsActive = table.Column<bool>(
                    type: "INTEGER",
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
                table.PrimaryKey("PK_People", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "RoleDefinitions",
            columns: table => new
            {
                Id = table.Column<int>(
                    type: "INTEGER",
                    nullable: false),
                Code = table.Column<string>(
                    type: "TEXT",
                    maxLength: 50,
                    nullable: false),
                NamePl = table.Column<string>(
                    type: "TEXT",
                    maxLength: 100,
                    nullable: false),
                DescriptionPl = table.Column<string>(
                    type: "TEXT",
                    maxLength: 1000,
                    nullable: false),
                IsSystem = table.Column<bool>(
                    type: "INTEGER",
                    nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RoleDefinitions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "UserAccounts",
            columns: table => new
            {
                Id = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                PersonId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                RoleDefinitionId = table.Column<int>(
                    type: "INTEGER",
                    nullable: false),
                LoginName = table.Column<string>(
                    type: "TEXT",
                    maxLength: 100,
                    nullable: false),
                NormalizedLoginName = table.Column<string>(
                    type: "TEXT",
                    maxLength: 100,
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
                LastLoginAtUtc = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserAccounts", x => x.Id);

                table.ForeignKey(
                    name: "FK_UserAccounts_People_PersonId",
                    column: x => x.PersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);

                table.ForeignKey(
                    name: "FK_UserAccounts_RoleDefinitions_RoleDefinitionId",
                    column: x => x.RoleDefinitionId,
                    principalTable: "RoleDefinitions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.InsertData(
            table: "RoleDefinitions",
            columns: new[]
            {
                "Id",
                "Code",
                "DescriptionPl",
                "IsSystem",
                "NamePl"
            },
            values: new object[,]
            {
                {
                    1,
                    "Administrator",
                    "Pełne zarządzanie Domio: użytkownicy, konfiguracja, finanse gospodarstwa, umowy, media i rozliczenia.",
                    true,
                    "Administrator"
                },
                {
                    2,
                    "HouseholdMember",
                    "Członek gospodarstwa domowego. Korzysta z funkcji domowych oraz własnych finansów zgodnie z nadanymi uprawnieniami.",
                    true,
                    "Domownik"
                },
                {
                    3,
                    "Tenant",
                    "Osoba objęta najmem. Dostęp do własnej umowy, rozliczeń, należności i płatności udostępnionych przez system.",
                    true,
                    "Lokator"
                },
                {
                    4,
                    "Guest",
                    "Ograniczony dostęp do wybranych informacji i funkcji bez uprawnień administracyjnych.",
                    true,
                    "Gość"
                },
                {
                    5,
                    "Child",
                    "Ograniczony profil członka gospodarstwa przeznaczony dla dziecka; zakres dostępu kontroluje administrator.",
                    true,
                    "Dziecko"
                }
            });

        migrationBuilder.CreateIndex(
            name: "IX_People_Email",
            table: "People",
            column: "Email");

        migrationBuilder.CreateIndex(
            name: "IX_People_LastName",
            table: "People",
            column: "LastName");

        migrationBuilder.CreateIndex(
            name: "IX_RoleDefinitions_Code",
            table: "RoleDefinitions",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserAccounts_NormalizedLoginName",
            table: "UserAccounts",
            column: "NormalizedLoginName",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserAccounts_PersonId",
            table: "UserAccounts",
            column: "PersonId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserAccounts_RoleDefinitionId",
            table: "UserAccounts",
            column: "RoleDefinitionId");

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
                    2026, 9, 8, 12, 43, 0, DateTimeKind.Utc),
                2
            });

        migrationBuilder.DropTable(
            name: "UserAccounts");

        migrationBuilder.DropTable(
            name: "People");

        migrationBuilder.DropTable(
            name: "RoleDefinitions");
    }
}
