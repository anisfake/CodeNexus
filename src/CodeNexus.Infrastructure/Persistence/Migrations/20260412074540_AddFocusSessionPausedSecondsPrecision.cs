using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFocusSessionPausedSecondsPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TotalPausedSeconds",
                table: "FocusSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
UPDATE [FocusSessions]
SET [TotalPausedSeconds] = CASE
    WHEN [TotalPausedMinutes] > 0 THEN [TotalPausedMinutes] * 60
    ELSE [TotalPausedSeconds]
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalPausedSeconds",
                table: "FocusSessions");
        }
    }
}
