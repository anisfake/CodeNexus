using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveQuizSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop foreign key, index and column for AISummary relationship on Quizzes
            migrationBuilder.DropForeignKey(
                name: "FK_Quizzes_AISummaries_SummaryId",
                table: "Quizzes");

            migrationBuilder.DropIndex(
                name: "IX_Quizzes_SummaryId",
                table: "Quizzes");

            migrationBuilder.DropColumn(
                name: "SummaryId",
                table: "Quizzes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-create SummaryId column, index and foreign key if rolling back
            migrationBuilder.AddColumn<Guid>(
                name: "SummaryId",
                table: "Quizzes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quizzes_SummaryId",
                table: "Quizzes",
                column: "SummaryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Quizzes_AISummaries_SummaryId",
                table: "Quizzes",
                column: "SummaryId",
                principalTable: "AISummaries",
                principalColumn: "SummaryId");
        }
    }
}
