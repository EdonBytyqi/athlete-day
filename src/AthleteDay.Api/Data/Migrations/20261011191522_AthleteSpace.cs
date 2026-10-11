using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AthleteDay.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AthleteSpace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Goals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AthleteId = table.Column<int>(type: "int", nullable: false),
                    TestType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Target = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SetBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Goals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Goals_Athletes_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "Athletes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProgressReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AthleteId = table.Column<int>(type: "int", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    To = table.Column<DateOnly>(type: "date", nullable: false),
                    Observations = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Achievements = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Challenges = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Recommendations = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Author = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: -1, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgressReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgressReviews_Athletes_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "Athletes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    AthleteId = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Author = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionNotes_Athletes_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "Athletes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SessionNotes_TrainingSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "TrainingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Goals_AthleteId_TestType",
                table: "Goals",
                columns: new[] { "AthleteId", "TestType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgressReviews_AthleteId_CreatedAt",
                table: "ProgressReviews",
                columns: new[] { "AthleteId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionNotes_AthleteId_CreatedAt",
                table: "SessionNotes",
                columns: new[] { "AthleteId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionNotes_SessionId",
                table: "SessionNotes",
                column: "SessionId");

            // Notes typed while logging move into session notes (ADR-12): same text, the logger as author.
            migrationBuilder.Sql("""
                INSERT INTO dbo.SessionNotes (SessionId, AthleteId, Text, Author, CreatedAt)
                SELECT Id, AthleteId, LEFT(Notes, 1000), LoggedBy, LoggedAt
                FROM dbo.TrainingSessions
                WHERE Notes IS NOT NULL AND LTRIM(RTRIM(Notes)) <> '';
                """);

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "TrainingSessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "TrainingSessions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // Back to one note per session: keep the first note of each.
            migrationBuilder.Sql("""
                UPDATE s SET Notes = LEFT(n.Text, 500)
                FROM dbo.TrainingSessions AS s
                CROSS APPLY (SELECT TOP (1) Text FROM dbo.SessionNotes WHERE SessionId = s.Id ORDER BY CreatedAt, Id) AS n;
                """);

            migrationBuilder.DropTable(
                name: "Goals");

            migrationBuilder.DropTable(
                name: "ProgressReviews");

            migrationBuilder.DropTable(
                name: "SessionNotes");
        }
    }
}
