using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFocusSessionForFlexiblePomodoro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DurationInMinutes",
                table: "FocusSessions",
                newName: "SessionStatus");

            migrationBuilder.AddColumn<int>(
                name: "ActualDurationMinutes",
                table: "FocusSessions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlannedDurationMinutes",
                table: "FocusSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualDurationMinutes",
                table: "FocusSessions");

            migrationBuilder.DropColumn(
                name: "PlannedDurationMinutes",
                table: "FocusSessions");

            migrationBuilder.RenameColumn(
                name: "SessionStatus",
                table: "FocusSessions",
                newName: "DurationInMinutes");
        }
    }
}
