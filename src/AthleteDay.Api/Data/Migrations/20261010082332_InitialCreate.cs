using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AthleteDay.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Athletes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sport = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BodyMassKg = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Athletes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    User = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Resource = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AthleteId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BlobName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    ImportedRows = table.Column<int>(type: "int", nullable: false),
                    RejectedRows = table.Column<int>(type: "int", nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceMeasurements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AthleteId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TestType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    ImportJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceMeasurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceMeasurements_Athletes_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "Athletes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NutritionTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AthleteId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    EnergyKcal = table.Column<int>(type: "int", nullable: false),
                    CarbsGPerKg = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: false),
                    KitchenNote = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SetBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NutritionTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NutritionTargets_Athletes_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "Athletes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhysioNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AthleteId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Restriction = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Author = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysioNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhysioNotes_Athletes_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "Athletes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainingSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AthleteId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Rpe = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LoggedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LoggedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingSessions_Athletes_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "Athletes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WellnessChecks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AthleteId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    SleepHours = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: false),
                    Soreness = table.Column<int>(type: "int", nullable: false),
                    Mood = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WellnessChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WellnessChecks_Athletes_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "Athletes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportRowErrors",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    Column = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RawLine = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportRowErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportRowErrors_ImportJobs_ImportJobId",
                        column: x => x.ImportJobId,
                        principalTable: "ImportJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Athletes_Code",
                table: "Athletes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_At",
                table: "AuditEntries",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceMeasurements_AthleteId_Date_TestType",
                table: "DeviceMeasurements",
                columns: new[] { "AthleteId", "Date", "TestType" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportJobs_UploadedAt",
                table: "ImportJobs",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ImportRowErrors_ImportJobId",
                table: "ImportRowErrors",
                column: "ImportJobId");

            migrationBuilder.CreateIndex(
                name: "IX_NutritionTargets_AthleteId_Date",
                table: "NutritionTargets",
                columns: new[] { "AthleteId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysioNotes_AthleteId_Date",
                table: "PhysioNotes",
                columns: new[] { "AthleteId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_TrainingSessions_AthleteId_Date",
                table: "TrainingSessions",
                columns: new[] { "AthleteId", "Date" })
                .Annotation("SqlServer:Include", new[] { "Rpe", "DurationMinutes" });

            migrationBuilder.CreateIndex(
                name: "IX_WellnessChecks_AthleteId_Date",
                table: "WellnessChecks",
                columns: new[] { "AthleteId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "DeviceMeasurements");

            migrationBuilder.DropTable(
                name: "ImportRowErrors");

            migrationBuilder.DropTable(
                name: "NutritionTargets");

            migrationBuilder.DropTable(
                name: "PhysioNotes");

            migrationBuilder.DropTable(
                name: "TrainingSessions");

            migrationBuilder.DropTable(
                name: "WellnessChecks");

            migrationBuilder.DropTable(
                name: "ImportJobs");

            migrationBuilder.DropTable(
                name: "Athletes");
        }
    }
}
