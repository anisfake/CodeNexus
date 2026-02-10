using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceTargetDateWithDurationDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetDate",
                table: "Goals");

            migrationBuilder.AddColumn<int>(
                name: "DurationDays",
                table: "Goals",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationDays",
                table: "Goals");

            migrationBuilder.AddColumn<DateTime>(
                name: "TargetDate",
                table: "Goals",
                type: "datetime2",
                nullable: true);
        }
    }
}
