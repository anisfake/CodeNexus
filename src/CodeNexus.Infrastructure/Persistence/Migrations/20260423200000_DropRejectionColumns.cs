using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    public partial class DropRejectionColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionCount",
                table: "LearningPathMentorReviews");

            migrationBuilder.DropColumn(
                name: "MaxRejections",
                table: "LearningPathMentorReviews");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxRejections",
                table: "LearningPathMentorReviews",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "RejectionCount",
                table: "LearningPathMentorReviews",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}

