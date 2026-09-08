using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domio.Infrastructure.Persistence.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SchemaVersions",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false),
                Version = table.Column<int>(type: "INTEGER", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SchemaVersions", x => x.Id);
            });

        migrationBuilder.InsertData(
            table: "SchemaVersions",
            columns: new[] { "Id", "UpdatedAtUtc", "Version" },
            values: new object[]
            {
                1,
                new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc),
                1
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SchemaVersions");
    }
}
