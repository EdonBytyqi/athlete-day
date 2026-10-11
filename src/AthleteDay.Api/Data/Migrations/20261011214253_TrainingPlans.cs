using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AthleteDay.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TrainingPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanDayChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AthleteId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ChangedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanDayChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanDayChanges_Athletes_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "Athletes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanTemplateSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AthleteId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Rpe = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanTemplateSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanTemplateSessions_Athletes_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "Athletes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanDayChangeSessions",
                columns: table => new
                {
                    PlanDayChangeId = table.Column<int>(type: "int", nullable: false),
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Rpe = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanDayChangeSessions", x => new { x.PlanDayChangeId, x.Id });
                    table.ForeignKey(
                        name: "FK_PlanDayChangeSessions_PlanDayChanges_PlanDayChangeId",
                        column: x => x.PlanDayChangeId,
                        principalTable: "PlanDayChanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanDayChanges_AthleteId_Date",
                table: "PlanDayChanges",
                columns: new[] { "AthleteId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanTemplateSessions_AthleteId_DayOfWeek_Position",
                table: "PlanTemplateSessions",
                columns: new[] { "AthleteId", "DayOfWeek", "Position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanDayChangeSessions");

            migrationBuilder.DropTable(
                name: "PlanTemplateSessions");

            migrationBuilder.DropTable(
                name: "PlanDayChanges");
        }
    }
}
