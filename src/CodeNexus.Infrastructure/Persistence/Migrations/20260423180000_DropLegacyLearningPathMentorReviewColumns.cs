using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyLearningPathMentorReviewColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Score",
                table: "LearningPathMentorReviews");

            migrationBuilder.DropColumn(
                name: "Feedback",
                table: "LearningPathMentorReviews");

            migrationBuilder.DropColumn(
                name: "Suggestions",
                table: "LearningPathMentorReviews");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Score",
                table: "LearningPathMentorReviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Feedback",
                table: "LearningPathMentorReviews",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Suggestions",
                table: "LearningPathMentorReviews",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
