using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Duration",
                table: "Goals",
                type: "int",
                nullable: false,
                defaultValue: 30); // Default to OneMonth (30 days)
                
            // Update existing goals to have OneMonth duration
            migrationBuilder.Sql("UPDATE Goals SET Duration = 30 WHERE Duration = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Duration",
                table: "Goals");
        }
    }
}
