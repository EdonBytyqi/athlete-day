using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AthleteDay.Api.Data.Migrations
{
    /// <summary>
    /// Free-text sports become a managed list (ADR-10). Hand-edited so no athlete loses its sport:
    /// the existing names become list entries before the text column goes.
    /// </summary>
    public partial class SportsList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    RetiredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sports_Name",
                table: "Sports",
                column: "Name",
                unique: true);

            // Every distinct free-text sport becomes an entry (case-insensitive, like the unique index).
            migrationBuilder.Sql("""
                INSERT INTO dbo.Sports (Name)
                SELECT DISTINCT LEFT(LTRIM(RTRIM(Sport)), 60) FROM dbo.Athletes WHERE LTRIM(RTRIM(Sport)) <> '';
                """);

            migrationBuilder.AddColumn<int>(
                name: "SportId",
                table: "Athletes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE a SET SportId = s.Id
                FROM dbo.Athletes AS a
                JOIN dbo.Sports AS s ON s.Name = LEFT(LTRIM(RTRIM(a.Sport)), 60);
                """);

            migrationBuilder.DropColumn(
                name: "Sport",
                table: "Athletes");

            migrationBuilder.CreateIndex(
                name: "IX_Athletes_SportId",
                table: "Athletes",
                column: "SportId");

            migrationBuilder.AddForeignKey(
                name: "FK_Athletes_Sports_SportId",
                table: "Athletes",
                column: "SportId",
                principalTable: "Sports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Athletes_Sports_SportId",
                table: "Athletes");

            migrationBuilder.DropIndex(
                name: "IX_Athletes_SportId",
                table: "Athletes");

            migrationBuilder.AddColumn<string>(
                name: "Sport",
                table: "Athletes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Back to free text: each athlete keeps the name of its sport.
            migrationBuilder.Sql("""
                UPDATE a SET Sport = s.Name
                FROM dbo.Athletes AS a
                JOIN dbo.Sports AS s ON s.Id = a.SportId;
                """);

            migrationBuilder.DropColumn(
                name: "SportId",
                table: "Athletes");

            migrationBuilder.DropTable(
                name: "Sports");
        }
    }
}
