using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotStartedStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Status column is nvarchar (enum stored as string)
            // Rename old values: Completed → NotPassed (existing data from before enum change)
            migrationBuilder.Sql("UPDATE QuizAttempts SET Status = 'NotPassed' WHERE Status = 'Completed'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE QuizAttempts SET Status = 'Completed' WHERE Status = 'NotPassed'");
        }
    }
}
