using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

public partial class AddPersonProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "ArchivedAtUtc",
            table: "People",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Notes",
            table: "People",
            type: "TEXT",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PersonTypeCode",
            table: "People",
            type: "TEXT",
            maxLength: 50,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "PersonProfiles",
            columns: table => new
            {
                PersonId = table.Column<Guid>(
                    type: "TEXT",
                    nullable: false),
                BirthDate = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: true),
                Pesel = table.Column<string>(
                    type: "TEXT",
                    maxLength: 11,
                    nullable: true),
                Nationality = table.Column<string>(
                    type: "TEXT",
                    maxLength: 100,
                    nullable: true),
                IdentityDocumentTypeCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 50,
                    nullable: true),
                IdentityDocumentNumber = table.Column<string>(
                    type: "TEXT",
                    maxLength: 100,
                    nullable: true),
                IdentityDocumentIssuingCountry = table.Column<string>(
                    type: "TEXT",
                    maxLength: 100,
                    nullable: true),
                IdentityDocumentIssuedOn = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: true),
                IdentityDocumentExpiresOn = table.Column<DateTime>(
                    type: "TEXT",
                    nullable: true),
                PreferredContactMethodCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 50,
                    nullable: true),
                CorrespondenceCountry = table.Column<string>(
                    type: "TEXT",
                    maxLength: 100,
                    nullable: true),
                CorrespondenceRegion = table.Column<string>(
                    type: "TEXT",
                    maxLength: 100,
                    nullable: true),
                CorrespondenceCity = table.Column<string>(
                    type: "TEXT",
                    maxLength: 100,
                    nullable: true),
                CorrespondencePostalCode = table.Column<string>(
                    type: "TEXT",
                    maxLength: 20,
                    nullable: true),
                CorrespondenceStreet = table.Column<string>(
                    type: "TEXT",
                    maxLength: 150,
                    nullable: true),
                CorrespondenceBuildingNumber = table.Column<string>(
                    type: "TEXT",
                    maxLength: 30,
                    nullable: true),
                CorrespondenceUnitNumber = table.Column<string>(
                    type: "TEXT",
                    maxLength: 30,
                    nullable: true),
                CorrespondenceNotes = table.Column<string>(
                    type: "TEXT",
                    maxLength: 500,
                    nullable: true),
                EmergencyContactFirstName = table.Column<string>(
                    type: "TEXT",
                    maxLength: 100,
                    nullable: true),
                EmergencyContactLastName = table.Column<string>(
                    type: "TEXT",
                    maxLength: 100,
                    nullable: true),
                EmergencyContactRelation = table.Column<string>(
                    type: "TEXT",
                    maxLength: 100,
                    nullable: true),
                EmergencyContactPhone = table.Column<string>(
                    type: "TEXT",
                    maxLength: 50,
                    nullable: true),
                EmergencyContactEmail = table.Column<string>(
                    type: "TEXT",
                    maxLength: 254,
                    nullable: true),
                EmergencyContactNotes = table.Column<string>(
                    type: "TEXT",
                    maxLength: 500,
                    nullable: true),
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
                    "PK_PersonProfiles",
                    x => x.PersonId);

                table.ForeignKey(
                    name: "FK_PersonProfiles_People_PersonId",
                    column: x => x.PersonId,
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_People_PersonTypeCode",
            table: "People",
            column: "PersonTypeCode");

        migrationBuilder.CreateIndex(
            name: "IX_PersonProfiles_Pesel",
            table: "PersonProfiles",
            column: "Pesel",
            unique: true);

        migrationBuilder.UpdateData(
            table: "SchemaVersions",
            keyColumn: "Id",
            keyValue: 1,
            columns: new[] { "UpdatedAtUtc", "Version" },
            values: new object[]
            {
                new DateTime(
                    2026, 9, 9, 8, 41, 0, DateTimeKind.Utc),
                7
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PersonProfiles");

        migrationBuilder.DropIndex(
            name: "IX_People_PersonTypeCode",
            table: "People");

        migrationBuilder.DropColumn(
            name: "ArchivedAtUtc",
            table: "People");

        migrationBuilder.DropColumn(
            name: "Notes",
            table: "People");

        migrationBuilder.DropColumn(
            name: "PersonTypeCode",
            table: "People");

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
}
