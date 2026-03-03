using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyFieldsAndUpdateSessionType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoalContent",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "GoalDeadline",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "GoalContent",
                table: "FocusSessions");

            migrationBuilder.DropColumn(
                name: "GoalDeadline",
                table: "FocusSessions");

            migrationBuilder.RenameColumn(
                name: "Duration",
                table: "FocusSessions",
                newName: "DurationInMinutes");

            migrationBuilder.AlterColumn<int>(
                name: "SessionType",
                table: "FocusSessions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DurationInMinutes",
                table: "FocusSessions",
                newName: "Duration");

            migrationBuilder.AddColumn<string>(
                name: "GoalContent",
                table: "Tasks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GoalDeadline",
                table: "Tasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SessionType",
                table: "FocusSessions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "GoalContent",
                table: "FocusSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GoalDeadline",
                table: "FocusSessions",
                type: "datetime2",
                nullable: true);
        }
    }
}
