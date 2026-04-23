using System;
using CodeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260423150000_AddDecisionFieldsToLearningPathMentorReviews")]
    public partial class AddDecisionFieldsToLearningPathMentorReviews : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DecisionStatus",
                table: "LearningPathMentorReviews",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTime>(
                name: "StudentDecidedAt",
                table: "LearningPathMentorReviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentDecisionNote",
                table: "LearningPathMentorReviews",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DecisionStatus",
                table: "LearningPathMentorReviews");

            migrationBuilder.DropColumn(
                name: "StudentDecidedAt",
                table: "LearningPathMentorReviews");

            migrationBuilder.DropColumn(
                name: "StudentDecisionNote",
                table: "LearningPathMentorReviews");
        }
    }
}
