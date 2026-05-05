using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateLearningpathMentorReviewTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new columns to existing LearningPathMentorReviews table
            migrationBuilder.AddColumn<Guid>(
                name: "RevisedPathId",
                table: "LearningPathMentorReviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangeSummary",
                table: "LearningPathMentorReviews",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangeReason",
                table: "LearningPathMentorReviews",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentRequestNote",
                table: "LearningPathMentorReviews",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RejectionCount",
                table: "LearningPathMentorReviews",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxRejections",
                table: "LearningPathMentorReviews",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddForeignKey(
                name: "FK_LearningPathMentorReviews_LearningPaths_RevisedPathId",
                table: "LearningPathMentorReviews",
                column: "RevisedPathId",
                principalTable: "LearningPaths",
                principalColumn: "PathId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningPathMentorReviews_RevisedPathId",
                table: "LearningPathMentorReviews",
                column: "RevisedPathId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LearningPathMentorReviews_LearningPaths_RevisedPathId",
                table: "LearningPathMentorReviews");

            migrationBuilder.DropIndex(
                name: "IX_LearningPathMentorReviews_RevisedPathId",
                table: "LearningPathMentorReviews");

            migrationBuilder.DropColumn(
                name: "RevisedPathId",
                table: "LearningPathMentorReviews");

            migrationBuilder.DropColumn(
                name: "ChangeSummary",
                table: "LearningPathMentorReviews");

            migrationBuilder.DropColumn(
                name: "ChangeReason",
                table: "LearningPathMentorReviews");

            migrationBuilder.DropColumn(
                name: "StudentRequestNote",
                table: "LearningPathMentorReviews");

            migrationBuilder.DropColumn(
                name: "RejectionCount",
                table: "LearningPathMentorReviews");

            migrationBuilder.DropColumn(
                name: "MaxRejections",
                table: "LearningPathMentorReviews");
        }
    }
}
