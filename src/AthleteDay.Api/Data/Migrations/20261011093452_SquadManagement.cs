using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AthleteDay.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SquadManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "Athletes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BirthYear",
                table: "Athletes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sex",
                table: "Athletes",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "NotStated"); // existing athletes: sex not stated
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Athletes");

            migrationBuilder.DropColumn(
                name: "BirthYear",
                table: "Athletes");

            migrationBuilder.DropColumn(
                name: "Sex",
                table: "Athletes");
        }
    }
}
